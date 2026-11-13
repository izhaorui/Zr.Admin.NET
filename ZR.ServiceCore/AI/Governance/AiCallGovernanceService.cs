using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using Infrastructure.Model;
using Microsoft.Extensions.Options;
using NLog;
using ZR.Model.AI;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 统一治理入口：主库策略解析、额度检查、并发控制及调用流水结算。
    /// 角色策略表示“具有该角色的每用户额度”，不作为角色成员共享池。
    /// 并发计数与在途额度经 CacheStore 原子加减：配置 Redis 时跨实例共享，否则回落进程内内存（单实例语义）。
    /// </summary>
    [AppService(ServiceType = typeof(IAiCallGovernance), ServiceLifetime = LifeTime.Transient)]
    public class AiCallGovernanceService : BaseService<AiAccessPolicy>, IAiCallGovernance
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        /// <summary>在途计数的过期时间（分钟）：正常结算会即时回滚；异常遗留的计数在空闲后自动回收。</summary>
        private const int ReservationExpireMinutes = 30;
        private const string ConcurrencyKeyPrefix = "ai:conc:";
        private const string InflightTokenKeyPrefix = "ai:inflight:tok:";
        private const string InflightAmountKeyPrefix = "ai:inflight:amt:";
        /// <summary>金额以百万分之一的整数计数，使在途金额可用原子 INCRBY 累加（向上取整，保守计入）。</summary>
        private const decimal AmountMicroScale = 1_000_000m;
        /// <summary>分钟限流键过期时间（分钟）：需覆盖一个分钟桶并留裕量，防止跨分钟边界丢计数。</summary>
        private const int ChatRateWindowExpireMinutes = 2;
        // ⚠️ 单实例/单库假设：计量断路器是进程内全局开关，任一 DB 读写失败即对所有租户/用户关闭普通 AI
        // 调用（fail-closed），由后续任一次成功落库自动恢复。多实例下各进程独立判定；若启用多租户分库，
        // 应改为按租户隔离的断路器，避免单个租户 DB 抖动熔断全站。
        private static volatile bool AccountingHealthy = true;
        /// <summary>
        /// 当前生效的 AI 配置：统一走解析器（数据库覆盖优先，否则文件）。
        /// 不缓存为字段——后台改完配置后无需重启即生效。
        /// </summary>
        private static AiOptions Options => AiOptionsResolver.Get();

        /// <summary>
        /// 开始一次 AI 调用治理流程，返回租约对象，调用方应在调用完成后调用 CompleteAsync 结算。
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<AiCallLease> BeginAsync(AiCallRequest request)
        {
            request ??= new AiCallRequest();
            var actor = AiCallActorScope.Current;
            var user = App.HttpContext?.GetCurrentUser();
            // 身份只取可信来源：后台/异步调用优先 AiCallActorScope（由可信代码显式声明），HTTP 请求回退登录上下文；
            // 不接受调用方在 AiCallRequest 中自报身份，避免越权归因或跨用户刷额度。
            var tenantId = Normalize(actor?.TenantId, App.GetCurrentTenantId());
            var userId = actor?.UserId > 0 ? actor.UserId : user?.UserId ?? 0;
            var userName = Normalize(actor?.UserName, user?.UserName);
            // 角色层策略依赖 RoleIds：HTTP 从登录上下文取，后台从 actor 快照取（缺则视为无角色）。
            var roleIds = actor?.RoleIds?.Count > 0
                ? actor.RoleIds
                : user?.Roles?.Select(x => x.RoleId).ToArray() ?? Array.Empty<long>();
            var scene = Normalize(request.Scene, "unknown").ToLowerInvariant();
            // 结算幂等键：RequestId 每租约生成一次，ai_call_log 上建有唯一索引 uk_ai_call_request，
            // 作用是防止"同一租约"在并发/重入结算时重复落库（CompleteAsync 先查后插 + 唯一索引兜底）。
            // 粒度是"每次 HTTP 尝试一条流水"：重试/换渠道在 AiLlmClientRetryWrapper（AiLlmClient 外层），
            // 每次尝试都重新 BeginGovernedCallAsync → 新 RequestId → 新租约，各自归属实际渠道与单价
            // （failover 会换 provider/model），唯一索引不去重重试——这是设计而非缺陷。
            // 失败尝试无 usage 时仅按输入估值记账（见 CompleteAsync），避免重试重复扣满额估值。
            var requestId = Guid.NewGuid().ToString("N");

            AiModelPrice price = null;
            try
            {
                // 价格表读多写极少，直接用 SqlSugar 查询缓存：缓存服务由 DbCache 配置决定
                // （启用 dbCache 走 Redis，否则内存），且 IsAutoRemoveDataCache=true 会在价格变更时自动清缓存。
                price = await Context.Queryable<AiModelPrice>()
                    .Where(x => x.Status == 0
                        && x.Provider.ToLower() == Normalize(request.Provider, "unknown").ToLowerInvariant()
                        && x.Model.ToLower() == Normalize(request.Model, "unknown").ToLowerInvariant())
                    .WithCache(60 * 5)
                    .FirstAsync();
            }
            catch (Exception ex)
            {
                // 读价失败（DB 或缓存后端异常）不再继续：金额额度会失去校验依据，用量还会被记成 0。
                // 与 GetPolicies / GetUsagePairAsync 同口径：记日志 + 置不健康 + 抛治理异常（fail-closed）。
                // 健康标志由后续任一次成功落库自动恢复（见 CompleteAsync）。
                Logger.Warn(ex, "读取 AI 模型价格失败，暂停普通 AI 调用");
                AccountingHealthy = false;
                throw new AiGovernanceDeniedException("governance_unavailable", "AI 计量服务暂不可用，请稍后重试");
            }

            var estimatedPrompt = Math.Max(0, request.EstimatedPromptTokens);
            var estimatedCompletion = Math.Max(0, request.MaxCompletionTokens);
            var reservedTokens = (long)estimatedPrompt + estimatedCompletion;
            var reservedAmount = CalculateAmount(price, estimatedPrompt, estimatedCompletion);
            // 输入-only 预留：失败调用（超时/取消等）无 usage 时只按输入估值记账，
            // 输出未知不计满额上界，避免高估与重试重复扣额（见 CompleteAsync）。
            var reservedPromptAmount = CalculateAmount(price, estimatedPrompt, 0);

            var lease = new AiCallLease
            {
                RequestId = requestId,
                TraceId = App.HttpContext?.TraceIdentifier,
                Scene = scene,
                Provider = Normalize(request.Provider, "unknown"),
                Model = Normalize(request.Model, "unknown"),
                TenantId = tenantId,
                UserId = userId,
                UserName = userName,
                IsStream = request.IsStream,
                StartedAt = DateTime.Now,
                InputPricePerMillion = price?.InputPricePerMillion ?? 0,
                OutputPricePerMillion = price?.OutputPricePerMillion ?? 0,
                Currency = Normalize(price?.Currency, "CNY"),
                ReservedTokens = reservedTokens,
                ReservedAmount = reservedAmount,
                ReservedPromptTokens = estimatedPrompt,
                ReservedPromptAmount = reservedPromptAmount
            };

            if (!Options.Enable)
            {
                await RejectAsync(lease, "disabled", "AI 功能当前已停用");
            }
            if (!AccountingHealthy)
            {
                await RejectAsync(lease, "governance_unavailable", "AI 计量服务暂不可用，请稍后重试");
            }

            var (globalLimits, tenantLimits, subjectLimits, allLimits) =
                await ResolvePolicyLayersAsync(tenantId, userId, roleIds, scene);

            if (allLimits.Any(x => x.Disabled))
            {
                await RejectAsync(lease, "disabled", "当前账号或场景未启用 AI 功能");
            }
            if (price == null && allLimits.Any(x => x.HasAmountLimit))
            {
                await RejectAsync(lease, "price_missing", "当前模型未配置单价，无法安全执行金额额度校验");
            }

            var now = DateTime.Now;
            var dayBegin = now.Date;
            var monthBegin = new DateTime(now.Year, now.Month, 1);

            // 分钟限流：CacheStore 原子计数（Redis 下为网络调用），本身即原子，不必占锁区，尽早拒绝。
            if (scene == AiSceneCatalog.AiChat && userId > 0
                && !TryAcquireChatRateSlot(tenantId, userId, now, out var rateMessage))
            {
                await RejectAsync(lease, "minute_calls", rateMessage);
            }

            // 额度用量预读：DB 查询先于预留完成，预留本身为跨实例原子加减，故预读不需要放进任何全局闸门。
            var probes = new List<QuotaProbe>();
            CollectProbes(probes, globalLimits, "global");
            CollectProbes(probes, tenantLimits, $"tenant:{tenantId}");
            CollectProbes(probes, subjectLimits, $"user:{tenantId}:{userId}");
            foreach (var probe in probes)
            {
                var needDay = probe.Limit.DailyTokenLimit.HasValue || probe.Limit.DailyAmountLimit.HasValue;
                var needMonth = probe.Limit.MonthlyTokenLimit.HasValue || probe.Limit.MonthlyAmountLimit.HasValue;
                if (!needDay && !needMonth) continue;
                // 日/月合并为一条条件聚合查询（日窗口是月窗口子集），按需裁剪周期，减少往返。
                var (day, month) = await GetUsagePairAsync(lease, probe.ScopeKey, probe.Limit.SceneFilter, dayBegin, monthBegin, needDay, needMonth);
                probe.Day = day;
                probe.Month = month;
            }

            var concurrencyKeys = new List<(string Key, int Limit)>();
            foreach (var limit in globalLimits)
                AddConcurrency(concurrencyKeys, $"global|{limit.SceneFilter}", limit.ConcurrentLimit);
            foreach (var limit in tenantLimits)
                AddConcurrency(concurrencyKeys, $"tenant:{tenantId}|{limit.SceneFilter}", limit.ConcurrentLimit);
            foreach (var limit in subjectLimits)
                AddConcurrency(concurrencyKeys, $"user:{tenantId}:{userId}|{limit.SceneFilter}", limit.ConcurrentLimit);
            concurrencyKeys = concurrencyKeys
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => (x.Key, x.Min(y => y.Limit)))
                .ToList();

            // 预留：跨实例原子加减后复核上限，超限则整体回滚；DB 用量预读已在此之前完成，无需进程内锁。
            var rejection = TryReserve(lease, probes, concurrencyKeys);
            if (rejection is { } denied)
            {
                // 拒答落库（DB 写入）：租约未占用任何预留，结算时不会重复释放
                await RejectAsync(lease, denied.Reason, denied.Message);
            }
            return lease;
        }

        /// <summary>
        /// 完成一次 AI 调用治理流程，记录调用结果和计量信息。
        /// </summary>
        /// <param name="lease"></param>
        /// <param name="outcome"></param>
        /// <returns></returns>
        public async Task CompleteAsync(AiCallLease lease, AiCallOutcome outcome)
        {
            if (lease == null) return;
            outcome ??= new AiCallOutcome { Status = "unknown", ErrorType = "unknown" };

            var totalTokens = outcome.TotalTokens;
            var estimatedAmount = 0m;
            // 无 usage 时的兜底记账分两档：
            //  - 成功：模型已产出但 Provider 未回 usage，按完整预留（输入+满额输出）保守记账；
            //  - 失败（超时/取消/流中断）：输出未知，只计必然已发送的输入估值，避免按输出上界高估，
            //    也避免重试"每尝试一条流水"时重复扣满额（每尝试独立租约，见 BeginAsync 注释）。
            var fallbackWithoutUsage = !outcome.HasUsage
                && (outcome.Success || outcome.Status is "timeout" or "cancelled"
                    || outcome.ErrorType is "stream_interrupted" or "client_cancelled");
            if (fallbackWithoutUsage)
            {
                string usageNote;
                if (outcome.Success)
                {
                    totalTokens = (int)Math.Min(int.MaxValue, lease.ReservedTokens);
                    estimatedAmount = lease.ReservedAmount;
                    outcome.Status = "metering_incomplete";
                    outcome.ErrorType = "usage_missing";
                    usageNote = "Provider 未返回 usage，按调用前预估值记账";
                }
                else
                {
                    totalTokens = (int)Math.Min(int.MaxValue, lease.ReservedPromptTokens);
                    estimatedAmount = lease.ReservedPromptAmount;
                    usageNote = "Provider 未返回 usage，失败调用仅按输入估值记账";
                }
                outcome.ErrorMessage = string.IsNullOrWhiteSpace(outcome.ErrorMessage)
                    ? usageNote
                    : $"{outcome.ErrorMessage}；{usageNote}";
            }
            var inputAmount = lease.InputPricePerMillion * outcome.PromptTokens / 1_000_000m;
            var outputAmount = lease.OutputPricePerMillion * outcome.CompletionTokens / 1_000_000m;
            if (outcome.HasUsage) estimatedAmount = inputAmount + outputAmount;
            try
            {
                // 直接插入：重入/并发双写由唯一索引 uk_ai_call_request 拦截，下方 catch 复查 RequestId
                // 命中即按幂等处理，省掉每次结算前的 AnyAsync 预查往返（重入本就罕见）。
                await Context.Insertable(new AiCallLog
                {
                    Scene = AiHelper.Truncate(lease.Scene, 64),
                    Provider = lease.Provider,
                    Model = lease.Model,
                    TenantId = lease.TenantId,
                    RequestId = AiHelper.Truncate(lease.RequestId, 64),
                    TraceId = AiHelper.Truncate(lease.TraceId, 64),
                    Success = outcome.Success ? 1 : 0,
                    Status = AiHelper.Truncate(outcome.Status ?? (outcome.Success ? "success" : "unknown"), 32),
                    ErrorType = AiHelper.Truncate(outcome.ErrorType, 64),
                    ErrorMsg = AiHelper.Truncate(outcome.ErrorMessage, 1000),
                    HttpStatusCode = outcome.HttpStatusCode,
                    DurationMs = Math.Max(0, (long)(DateTime.Now - lease.StartedAt).TotalMilliseconds),
                    ProviderRequestId = AiHelper.Truncate(outcome.ProviderRequestId, 128),
                    IsStream = lease.IsStream ? 1 : 0,
                    PromptTokens = outcome.PromptTokens,
                    CompletionTokens = outcome.CompletionTokens,
                    TotalTokens = totalTokens,
                    HasUsage = outcome.HasUsage ? 1 : 0,
                    InputAmount = inputAmount,
                    OutputAmount = outputAmount,
                    EstimatedAmount = estimatedAmount,
                    Currency = lease.Currency ?? "CNY",
                    UserId = lease.UserId,
                    UserName = lease.UserName
                }).ExecuteReturnSnowflakeIdAsync();
                AccountingHealthy = true;
            }
            catch (Exception ex)
            {
                // 并发/重入下同一 RequestId 双写时，唯一索引 uk_ai_call_request 会让其中一方插入失败。
                // 该租约已落库属于"幂等命中"，不能当作计量故障 —— 否则一次并发写失败会把全局会计标记
                // 置为不健康，熔断全站普通 AI 调用（要到下一次成功落库才恢复）。
                // 判定刻意不匹配各库各异的唯一键错误码，改为复查该 RequestId 是否已存在：与数据库无关，
                // 插入前不再预查，唯一键冲突即幂等命中（复查自身失败则走不健康分支兜底）。
                var persisted = false;
                try
                {
                    persisted = await Context.Queryable<AiCallLog>().AnyAsync(x => x.RequestId == lease.RequestId);
                }
                catch (Exception probeEx)
                {
                    // 复查自身失败（DB 不可用等）不得空 catch 吞掉：记录后走不健康分支
                    Logger.Warn(probeEx, $"AI 调用流水唯一键冲突复查失败 requestId={lease.RequestId}");
                }

                if (persisted)
                {
                    AccountingHealthy = true;
                    Logger.Info($"AI 调用流水已由并发方写入，按幂等命中处理 requestId={lease.RequestId}");
                }
                else
                {
                    AccountingHealthy = false;
                    Logger.Warn(ex, $"写入 AI 调用治理流水失败 requestId={lease.RequestId}");
                }
            }
            finally
            {
                Release(lease);
            }
        }

        /// <summary>
        /// 获取当前用户在指定场景下的 AI 额度快照，包含已用额度和剩余额度。
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public async Task<AiQuotaSnapshot> GetMyQuotaAsync(string scene = AiSceneCatalog.AiChat)
        {
            scene = Normalize(scene, AiSceneCatalog.AiChat).ToLowerInvariant();
            var actor = AiCallActorScope.Current;
            var user = App.HttpContext?.GetCurrentUser();
            var tenantId = Normalize(actor?.TenantId, App.GetCurrentTenantId());
            var userId = actor?.UserId > 0 ? actor.UserId : user?.UserId ?? 0;
            // 与 BeginAsync 同口径：角色层策略也需覆盖后台/异步调用（actor 快照优先）。
            var roleIds = actor?.RoleIds?.Count > 0
                ? actor.RoleIds
                : user?.Roles?.Select(x => x.RoleId).ToArray() ?? Array.Empty<long>();

            var snapshot = new AiQuotaSnapshot
            {
                Scene = scene,
                Allowed = true,
                Currency = "CNY"
            };

            if (!Options.Enable)
            {
                snapshot.Allowed = false;
                snapshot.DeniedReason = "AI 功能当前已停用";
                return snapshot;
            }

            var (_, _, _, allLimits) = await ResolvePolicyLayersAsync(tenantId, userId, roleIds, scene);

            if (allLimits.Any(x => x.Disabled))
            {
                snapshot.Allowed = false;
                snapshot.DeniedReason = "当前账号或场景未启用 AI 功能";
            }

            snapshot.DailyTokenLimit = Min(allLimits.Select(x => x.DailyTokenLimit));
            snapshot.MonthlyTokenLimit = Min(allLimits.Select(x => x.MonthlyTokenLimit));
            snapshot.DailyAmountLimit = Min(allLimits.Select(x => x.DailyAmountLimit));
            snapshot.MonthlyAmountLimit = Min(allLimits.Select(x => x.MonthlyAmountLimit));

            var sceneFilter = allLimits.Any(x => !string.IsNullOrWhiteSpace(x.SceneFilter) && x.SceneFilter != "*")
                ? scene
                : "*";
            var lease = new AiCallLease { TenantId = tenantId, UserId = userId };
            var now = DateTime.Now;
            var (dayUsage, monthUsage) = await GetUsagePairAsync(lease, $"user:{tenantId}:{userId}", sceneFilter, now.Date, new DateTime(now.Year, now.Month, 1), true, true);
            snapshot.DailyTokenUsed = dayUsage.Tokens;
            snapshot.DailyAmountUsed = dayUsage.Amount;
            snapshot.MonthlyTokenUsed = monthUsage.Tokens;
            snapshot.MonthlyAmountUsed = monthUsage.Amount;
            return snapshot;
        }

        private async Task<(List<PolicyLimit> Global, List<PolicyLimit> Tenant, List<PolicyLimit> Subject, List<PolicyLimit> All)>
            ResolvePolicyLayersAsync(string tenantId, long userId, IReadOnlyList<long> roleIds, string scene)
        {
            var policies = await GetPoliciesAsync(tenantId);
            var globalLimits = ResolveConstraints(policies, "global", string.Empty, [0], scene);
            var tenantLimits = ResolveConstraints(policies, "tenant", tenantId, [0], scene);
            var roleLimits = ResolveConstraints(policies, "role", tenantId, roleIds, scene);
            var userLimits = ResolveConstraints(policies, "user", tenantId, [userId], scene);
            var subjectLimits = roleLimits.Concat(userLimits).ToList();
            if (subjectLimits.Count == 0 && scene == AiSceneCatalog.AiChat && Options.DefaultUserTotalTokens > 0)
            {
                subjectLimits.Add(new PolicyLimit
                {
                    HasPolicy = true,
                    MonthlyTokenLimit = Options.DefaultUserTotalTokens,
                    SceneFilter = AiSceneCatalog.AiChat
                });
            }
            var allLimits = globalLimits.Concat(tenantLimits).Concat(subjectLimits).ToList();
            return (globalLimits, tenantLimits, subjectLimits, allLimits);
        }

        /// <summary>
        /// 读取生效中的治理策略。策略表读多写极少，用 SqlSugar 查询缓存（缓存服务由 DbCache 配置决定：
        /// 启用 dbCache 走 Redis，否则内存）；策略增删改由 IsAutoRemoveDataCache 自动清缓存，无需手动失效。
        /// 读取失败 fail-closed（与价格 / 用量读取同口径）。
        /// </summary>
        private async Task<List<AiAccessPolicy>> GetPoliciesAsync(string tenantId)
        {
            try
            {
                return await Context.Queryable<AiAccessPolicy>()
                    .Where(x => x.Status == 0 && (x.ScopeType == "global" || x.TenantId == tenantId))
                    .WithCache(30)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"读取 AI 治理策略失败，已关闭普通 AI 调用 tenantId={tenantId}");
                AccountingHealthy = false;
                throw new AiGovernanceDeniedException("governance_unavailable", "AI 治理策略暂不可用，请稍后重试");
            }
        }

        /// <summary>把某一层级的有效额度策略收集为校验探针（保留 全局 → 租户 → 用户 的判定顺序）。</summary>
        private static void CollectProbes(List<QuotaProbe> target, IEnumerable<PolicyLimit> limits, string scopeKey)
        {
            foreach (var limit in limits)
            {
                if (!limit.HasAnyLimit) continue;
                target.Add(new QuotaProbe
                {
                    Limit = limit,
                    ScopeKey = scopeKey,
                    ReservationKey = $"{scopeKey}|{limit.SceneFilter}"
                });
            }
        }

        /// <summary>
        /// 一条 SQL 取回某 scope 的「当天/当月」用量：日窗口是月窗口子集，故取月窗口 SUM，
        /// 再用 IIF(CreateTime&gt;=dayBegin) 条件 SUM 同场取日，把原来日/月两条查询合并为一条。
        /// 按需裁剪窗口：仅日→日窗口；含月→月窗口。调用方只取自己需要的周期。
        /// </summary>
        private async Task<(UsageValue Day, UsageValue Month)> GetUsagePairAsync(
            AiCallLease lease, string scopeKey, string sceneFilter, DateTime dayBegin, DateTime monthBegin, bool needDay, bool needMonth)
        {
            try
            {
                // 全局额度要跨租户汇总，必须清掉 AiCallLog 的租户 QueryFilter。
                var begin = needMonth ? monthBegin : dayBegin;
                var query = Context.Queryable<AiCallLog>().ClearFilter()
                    .Where(x => x.CreateTime >= begin && x.Status != "rejected");
                if (scopeKey.StartsWith("tenant:", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.TenantId == lease.TenantId);
                }
                else if (scopeKey.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => x.TenantId == lease.TenantId && x.UserId == lease.UserId);
                }
                if (!string.IsNullOrWhiteSpace(sceneFilter) && sceneFilter != "*")
                {
                    query = query.Where(x => x.Scene == sceneFilter);
                }
                // 月合计 + 日条件合计同场返回（IIF 条件聚合写法与 SysAiUsageService 等一致）。
                var row = await query
                    .Select(x => new UsagePairRow
                    {
                        MonthTokens = SqlFunc.AggregateSum(x.TotalTokens),
                        MonthAmount = SqlFunc.AggregateSum(x.EstimatedAmount),
                        DayTokens = SqlFunc.AggregateSum(SqlFunc.IIF(x.CreateTime >= dayBegin, x.TotalTokens, 0)),
                        DayAmount = SqlFunc.AggregateSum(SqlFunc.IIF(x.CreateTime >= dayBegin, x.EstimatedAmount, 0m))
                    })
                    .FirstAsync();
                var day = new UsageValue { Tokens = row?.DayTokens ?? 0, Amount = row?.DayAmount ?? 0 };
                var month = new UsageValue { Tokens = row?.MonthTokens ?? 0, Amount = row?.MonthAmount ?? 0 };
                return (day, month);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"读取 AI 累计额度失败 scope={scopeKey}");
                AccountingHealthy = false;
                throw new AiGovernanceDeniedException("governance_unavailable", "AI 额度计量暂不可用，请稍后重试");
            }
        }

        /// <summary>
        /// 跨实例原子预留：对全部配额范围键的 Token/金额在途计数与并发键做原子加减，再逐条复核上限。
        /// 返回 null 表示预留成功（并把占用的键记入租约，结算时释放）；否则返回拒绝原因并回滚本次已加项。
        /// 缓存异常时同样回滚并以 governance_unavailable 拒绝（fail-closed）。
        /// </summary>
        private static (string Reason, string Message)? TryReserve(AiCallLease lease,
            IReadOnlyList<QuotaProbe> probes, IReadOnlyList<(string Key, int Limit)> concurrencyKeys)
        {
            var scopeKeys = BuildQuotaScopeKeys(lease.TenantId, lease.UserId, lease.Scene)
                .Concat(probes.Select(x => x.ReservationKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var cache = CacheStore.Default;
            var applied = new List<(string Key, long Delta)>();
            var amountMicro = ToAmountMicro(lease.ReservedAmount);
            try
            {
                var tokenAfter = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                var amountAfter = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var scopeKey in scopeKeys)
                {
                    tokenAfter[scopeKey] = Apply(cache, applied, InflightTokenKeyPrefix + scopeKey, lease.ReservedTokens);
                    amountAfter[scopeKey] = Apply(cache, applied, InflightAmountKeyPrefix + scopeKey, amountMicro);
                }

                var concurrencyAfter = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in concurrencyKeys)
                {
                    concurrencyAfter[item.Key] = Apply(cache, applied, ConcurrencyKeyPrefix + item.Key, 1);
                }

                var denied = CheckReservation(probes, tokenAfter, amountAfter, concurrencyKeys, concurrencyAfter);
                if (denied != null)
                {
                    Rollback(cache, applied);
                    return denied;
                }

                lease.ReservationScopeKeys = scopeKeys;
                lease.ConcurrencyKeys = concurrencyKeys.Select(x => x.Key).ToArray();
                return null;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"AI 额度与并发预留失败，已回滚并拒绝调用 requestId={lease.RequestId}");
                Rollback(cache, applied);
                return ("governance_unavailable", "AI 额度与并发服务暂不可用，请稍后重试");
            }
        }

        /// <summary>复核预留后的用量：探针顺序与拒绝原因保持原有口径（先额度后并发）。</summary>
        private static (string Reason, string Message)? CheckReservation(IReadOnlyList<QuotaProbe> probes,
            IReadOnlyDictionary<string, long> tokenAfter, IReadOnlyDictionary<string, long> amountAfter,
            IReadOnlyList<(string Key, int Limit)> concurrencyKeys, IReadOnlyDictionary<string, long> concurrencyAfter)
        {
            foreach (var probe in probes)
            {
                var limit = probe.Limit;
                var tokens = tokenAfter[probe.ReservationKey];
                var amount = amountAfter[probe.ReservationKey] / AmountMicroScale;
                if (limit.DailyTokenLimit.HasValue && probe.Day.Tokens + tokens > limit.DailyTokenLimit.Value)
                {
                    return ("daily_token", "今日 AI Token 额度已用尽");
                }
                if (limit.MonthlyTokenLimit.HasValue && probe.Month.Tokens + tokens > limit.MonthlyTokenLimit.Value)
                {
                    return ("monthly_token", "本月 AI Token 额度已用尽");
                }
                if (limit.DailyAmountLimit.HasValue && probe.Day.Amount + amount > limit.DailyAmountLimit.Value)
                {
                    return ("daily_amount", "今日 AI 金额额度已用尽");
                }
                if (limit.MonthlyAmountLimit.HasValue && probe.Month.Amount + amount > limit.MonthlyAmountLimit.Value)
                {
                    return ("monthly_amount", "本月 AI 金额额度已用尽");
                }
            }

            foreach (var item in concurrencyKeys)
            {
                if (concurrencyAfter[item.Key] > item.Limit)
                {
                    return ("concurrency", "AI 并发请求数已达上限，请稍后重试");
                }
            }
            return null;
        }

        private static long Apply(ICache cache, List<(string Key, long Delta)> applied, string key, long delta)
        {
            var after = cache.IncrementBy(key, delta, ReservationExpireMinutes);
            applied.Add((key, delta));
            return after;
        }

        /// <summary>回滚本次已成功加上的计数；回滚失败只记日志，由过期回收兜底。</summary>
        private static void Rollback(ICache cache, List<(string Key, long Delta)> applied)
        {
            foreach (var (key, delta) in applied)
            {
                try
                {
                    cache.IncrementBy(key, -delta, ReservationExpireMinutes);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"AI 在途计数回滚失败，依赖过期回收 key={key}");
                }
            }
            applied.Clear();
        }

        private static long ToAmountMicro(decimal amount) =>
            (long)Math.Ceiling(Math.Max(0m, amount) * AmountMicroScale);

        /// <summary>分钟限流计数键：按「租户 + 用户 + 分钟桶」隔离，分钟切换即自然开新窗口。</summary>
        private static string ChatRateKey(string tenantId, long userId, DateTime now) =>
            $"ai:chatrate:{tenantId}:{userId}:{now:yyyyMMddHHmm}";

        /// <summary>
        /// 尝试占用一个分钟限流额度：基于 CacheStore 原子自增，超过上限返回 false。
        /// RedisServer:open=1 时为跨节点全局计数，否则自动回落进程内内存（单实例语义）。
        /// 计数异常时放行（fail-open），避免缓存抖动熔断全部对话。
        /// </summary>
        private bool TryAcquireChatRateSlot(string tenantId, long userId, DateTime now, out string message)
        {
            message = null;
            var limit = Options.ChatRateLimitPerMinute;
            if (limit <= 0) return true;
            long count;
            try
            {
                count = CacheStore.Default.Increment(ChatRateKey(tenantId, userId, now), ChatRateWindowExpireMinutes);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"AI 分钟限流计数失败，本次放行 tenantId={tenantId} userId={userId}");
                return true;
            }
            if (count <= limit) return true;
            message = $"发送过于频繁，请稍后再试（每分钟最多 {limit} 次模型调用）";
            return false;
        }

        private static IReadOnlyList<string> BuildQuotaScopeKeys(string tenantId, long userId, string scene) =>
            [
                "global|*", $"global|{scene}",
                $"tenant:{tenantId}|*", $"tenant:{tenantId}|{scene}",
                $"user:{tenantId}:{userId}|*", $"user:{tenantId}:{userId}|{scene}"
            ];

        private static void AddConcurrency(List<(string Key, int Limit)> items, string key, int? limit)
        {
            if (limit > 0) items.Add((key, limit.Value));
        }

        /// <summary>
        /// 结算时释放在途预留与并发占用。释放后清空租约上的键列表，重复调用无副作用（幂等）。
        /// </summary>
        private static void Release(AiCallLease lease)
        {
            if (lease.ReservationScopeKeys.Count == 0 && lease.ConcurrencyKeys.Count == 0) return;
            var cache = CacheStore.Default;
            var amountMicro = ToAmountMicro(lease.ReservedAmount);
            try
            {
                foreach (var scopeKey in lease.ReservationScopeKeys)
                {
                    cache.IncrementBy(InflightTokenKeyPrefix + scopeKey, -lease.ReservedTokens, ReservationExpireMinutes);
                    cache.IncrementBy(InflightAmountKeyPrefix + scopeKey, -amountMicro, ReservationExpireMinutes);
                }
                foreach (var key in lease.ConcurrencyKeys)
                {
                    cache.IncrementBy(ConcurrencyKeyPrefix + key, -1, ReservationExpireMinutes);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"释放 AI 在途预留失败，依赖过期回收 requestId={lease.RequestId}");
            }
            finally
            {
                lease.ReservationScopeKeys = Array.Empty<string>();
                lease.ConcurrencyKeys = Array.Empty<string>();
            }
        }

        private async Task RejectAsync(AiCallLease lease, string reason, string message)
        {
            await RecordRejectedWithoutThrowAsync(lease, reason, message);
            throw new AiGovernanceDeniedException(reason, message);
        }

        private Task RecordRejectedWithoutThrowAsync(AiCallLease lease, string reason, string message) =>
            CompleteAsync(lease, new AiCallOutcome
            {
                Success = false,
                Status = "rejected",
                ErrorType = reason,
                ErrorMessage = message
            });

        internal static List<PolicyLimit> ResolveConstraints(
            IEnumerable<AiAccessPolicy> policies, string scopeType, string tenantId,
            IReadOnlyList<long> subjectIds, string scene)
        {
            var ids = subjectIds?.ToHashSet() ?? new HashSet<long>();
            return policies
                .Where(x => string.Equals(x.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.TenantId ?? string.Empty, tenantId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && ids.Contains(x.SubjectId)
                    && (x.Scene == "*" || string.Equals(x.Scene, scene, StringComparison.OrdinalIgnoreCase)))
                .Select(FromPolicy)
                .ToList();
        }

        private static PolicyLimit FromPolicy(AiAccessPolicy policy) => policy == null
            ? PolicyLimit.Empty
            : new PolicyLimit
            {
                HasPolicy = true,
                Disabled = policy.IsEnabled == 0,
                SceneFilter = string.IsNullOrWhiteSpace(policy.Scene) ? "*" : policy.Scene,
                DailyTokenLimit = Positive(policy.DailyTokenLimit),
                MonthlyTokenLimit = Positive(policy.MonthlyTokenLimit),
                DailyAmountLimit = Positive(policy.DailyAmountLimit),
                MonthlyAmountLimit = Positive(policy.MonthlyAmountLimit),
                ConcurrentLimit = Positive(policy.ConcurrentLimit)
            };

        private static long? Positive(long? value) => value > 0 ? value : null;
        private static int? Positive(int? value) => value > 0 ? value : null;
        private static decimal? Positive(decimal? value) => value > 0 ? value : null;
        private static T? Min<T>(IEnumerable<T?> values) where T : struct, IComparable<T>
        {
            var present = values.Where(x => x.HasValue).Select(x => x.Value).ToList();
            return present.Count == 0 ? null : present.Min();
        }

        internal static decimal CalculateAmount(AiModelPrice price, int prompt, int completion) =>
            price == null ? 0 : price.InputPricePerMillion * prompt / 1_000_000m
                + price.OutputPricePerMillion * completion / 1_000_000m;

        private static string Normalize(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value.Trim();

        internal sealed class PolicyLimit
        {
            public static readonly PolicyLimit Empty = new();
            public bool HasPolicy { get; set; }
            public bool Disabled { get; set; }
            public string SceneFilter { get; set; } = "*";
            public long? DailyTokenLimit { get; set; }
            public long? MonthlyTokenLimit { get; set; }
            public decimal? DailyAmountLimit { get; set; }
            public decimal? MonthlyAmountLimit { get; set; }
            public int? ConcurrentLimit { get; set; }
            public bool HasAnyLimit => DailyTokenLimit.HasValue || MonthlyTokenLimit.HasValue
                || DailyAmountLimit.HasValue || MonthlyAmountLimit.HasValue;
            public bool HasAmountLimit => DailyAmountLimit.HasValue || MonthlyAmountLimit.HasValue;
        }

        private sealed class UsageValue
        {
            public long Tokens { get; set; }
            public decimal Amount { get; set; }
        }

        /// <summary>
        /// 额度校验探针：预读「当天/当月」已完成用量，预留后再结合在途计数做复核，
        /// 使 DB 查询不落在全局串行区内；只预读真正配了额度的周期。
        /// </summary>
        private sealed class QuotaProbe
        {
            public PolicyLimit Limit { get; set; }
            public string ScopeKey { get; set; }
            public string ReservationKey { get; set; }
            public UsageValue Day { get; set; } = new();
            public UsageValue Month { get; set; } = new();
        }

        /// <summary>
        /// 用量聚合查询投影：一条 SQL 同时返回「当天/当月」的 Token / 金额合计。
        /// 属性可空——区间内没有记录时 SUM 返回 NULL（SqlSugar 会把 NULL 映射为 null）。
        /// 公开类型：SqlSugar 构造投影对象需要类型可访问。
        /// </summary>
        public sealed class UsagePairRow
        {
            public long? DayTokens { get; set; }
            public decimal? DayAmount { get; set; }
            public long? MonthTokens { get; set; }
            public decimal? MonthAmount { get; set; }
        }
    }
}
