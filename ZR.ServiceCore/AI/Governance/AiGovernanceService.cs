using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ZR.Model;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.Model.System.Enums;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.AI.IService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 治理管理（策略额度、模型价格、Provider 诊断）。
    /// 权限边界：菜单/接口级权限码校验由 AiGovernanceController 的 ActionPermissionFilter 统一负责（单一闸门），
    /// 本服务不重复校验，只做过滤器无法表达的两类判断：
    /// ① 数据归属——策略属于哪个租户、是否 global/tenant 级（AuthorizePolicy）；
    /// ② 请求体语义——PUT 传 Id&lt;=0 时按"新增"而非"修改"要求权限（SavePolicy）。
    /// 非 HTTP 调用方（后台任务、其他模块）须自行鉴权后再调用本服务。
    /// </summary>
    [AppService(ServiceType = typeof(IAiGovernanceService), ServiceLifetime = LifeTime.Transient)]
    public class AiGovernanceService : BaseService<AiAccessPolicy>, IAiGovernanceService
    {
        private static readonly HashSet<string> ScopeTypes = new(StringComparer.OrdinalIgnoreCase)
            { "global", "tenant", "role", "user" };

        /// <summary>治理策略新增权限（PUT 传 Id&lt;=0 时按新增判，见 SavePolicy）</summary>
        private const string PermPolicyAdd = "ai:governance:add";
        /// <summary>治理策略修改权限</summary>
        private const string PermPolicyEdit = "ai:governance:edit";

        private readonly IAiCallGovernance _callGovernance;
        private readonly ISysRoleService _roleService;
        private readonly AiOptions _options;

        public AiGovernanceService(
            IAiCallGovernance callGovernance,
            ISysRoleService roleService,
            IOptions<OptionsSetting> options)
        {
            _callGovernance = callGovernance;
            _roleService = roleService;
            _options = options.Value?.AiOptions ?? new AiOptions();
        }

        public PagedInfo<AiPolicyListDto> GetPolicyList(AiPolicyQueryDto query)
        {
            query ??= new AiPolicyQueryDto();
            var currentTenant = App.GetCurrentTenantId();
            var platform = AiPermissionHelper.IsPlatformAdmin();
            var pageNum = Math.Max(1, query.PageNum);
            var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
            var total = 0;
            var list = Queryable()
                .WhereIF(!platform, x => x.TenantId == currentTenant && (x.ScopeType == "role" || x.ScopeType == "user"))
                .WhereIF(platform && !string.IsNullOrWhiteSpace(query.TenantId), x => x.TenantId == query.TenantId)
                .WhereIF(!string.IsNullOrWhiteSpace(query.ScopeType), x => x.ScopeType == query.ScopeType)
                .WhereIF(query.SubjectId.HasValue, x => x.SubjectId == query.SubjectId.Value)
                .WhereIF(!string.IsNullOrWhiteSpace(query.Scene), x => x.Scene == query.Scene)
                .WhereIF(query.Status.HasValue, x => x.Status == query.Status.Value)
                .OrderBy(x => x.Id, global::SqlSugar.OrderByType.Desc)
                .ToPageList(pageNum, pageSize, ref total);
            return new PagedInfo<AiPolicyListDto>
            {
                PageIndex = pageNum,
                PageSize = pageSize,
                TotalNum = total,
                Result = FillPolicyNames(list)
            };
        }

        public AiAccessPolicy GetPolicy(long id)
        {
            var entity = FindPolicy(id);
            AuthorizePolicy(entity);
            return entity;
        }

        public List<AiPolicySubjectDto> GetPolicySubjects(string scopeType, string keyword)
        {
            var key = (keyword ?? string.Empty).Trim();
            if (string.Equals(scopeType, "role", StringComparison.OrdinalIgnoreCase))
            {
                return _roleService.Queryable()
                    .Where(r => r.DelFlag == DelFlagEnum.Normal)
                    .WhereIF(!string.IsNullOrEmpty(key), r => r.RoleName.Contains(key) || r.RoleKey.Contains(key))
                    .OrderBy(r => r.RoleSort)
                    .Take(200)
                    .ToList()
                    .Select(r => new AiPolicySubjectDto { Id = r.RoleId, Name = r.RoleName, Extra = r.RoleKey })
                    .ToList();
            }
            return new List<AiPolicySubjectDto>();
        }

        public long SavePolicy(AiPolicySaveDto input)
        {
            if (input == null) throw new CustomException("策略参数不能为空");
            // 控制器只按 URL 判权限（PUT=edit、POST=add）；PUT 传 Id<=0 实际是新增，
            // 只有服务层看得到请求体里的 Id，故此处按实际操作类型再判一次。
            EnsurePermission(input.Id > 0 ? PermPolicyEdit : PermPolicyAdd);
            NormalizePolicy(input);
            ValidatePolicy(input);

            var duplicate = Queryable().Any(x => x.Id != input.Id && x.ScopeType == input.ScopeType
                && x.TenantId == input.TenantId && x.SubjectId == input.SubjectId && x.Scene == input.Scene);
            if (duplicate) throw new CustomException("相同作用域和场景的 AI 策略已存在");

            if (input.Id > 0)
            {
                var entity = FindPolicy(input.Id) ?? throw new CustomException("AI 策略不存在");
                AuthorizePolicy(entity);
                MapPolicy(input, entity);
                entity.Update_by = App.UserName;
                entity.Update_time = DateTime.Now;
                Context.Updateable(entity).ExecuteCommand();
                return entity.Id;
            }

            var added = new AiAccessPolicy();
            MapPolicy(input, added);
            added.Create_by = App.UserName;
            added.Create_time = DateTime.Now;
            return Context.Insertable(added).ExecuteReturnIdentity();
        }

        public int DeletePolicy(long id)
        {
            var entity = FindPolicy(id) ?? throw new CustomException("AI 策略不存在");
            AuthorizePolicy(entity);
            return Context.Deleteable<AiAccessPolicy>().Where(x => x.Id == id).ExecuteCommand();
        }

        public PagedInfo<AiModelPrice> GetPriceList(AiModelPriceQueryDto query)
        {
            EnsurePlatformAdmin();
            query ??= new AiModelPriceQueryDto();
            var pageNum = Math.Max(1, query.PageNum);
            var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
            var total = 0;
            var list = Context.Queryable<AiModelPrice>()
                .WhereIF(!string.IsNullOrWhiteSpace(query.Provider), x => x.Provider == query.Provider)
                .WhereIF(!string.IsNullOrWhiteSpace(query.Model), x => x.Model.Contains(query.Model))
                .WhereIF(query.Status.HasValue, x => x.Status == query.Status.Value)
                .OrderBy(x => x.Id, global::SqlSugar.OrderByType.Desc)
                .ToPageList(pageNum, pageSize, ref total);
            return new PagedInfo<AiModelPrice>
            {
                PageIndex = pageNum,
                PageSize = pageSize,
                TotalNum = total,
                Result = list
            };
        }

        public AiModelPrice GetPrice(long id)
        {
            EnsurePlatformAdmin();
            return Context.Queryable<AiModelPrice>().First(x => x.Id == id);
        }

        public long SavePrice(AiModelPriceSaveDto input)
        {
            EnsurePlatformAdmin();
            if (input == null) throw new CustomException("模型价格参数不能为空");
            var provider = (input.Provider ?? string.Empty).Trim().ToLowerInvariant();
            var model = (input.Model ?? string.Empty).Trim();
            var currency = (input.Currency ?? "CNY").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(model)) throw new CustomException("Provider 和模型不能为空");
            if (provider.Length > 32) throw new CustomException("Provider 最多 32 个字符");
            if (model.Length > 100) throw new CustomException("模型名称最多 100 个字符");
            if (input.InputPricePerMillion < 0 || input.OutputPricePerMillion < 0) throw new CustomException("模型单价不能小于 0");
            if (currency != "CNY") throw new CustomException("当前额度金额统一按人民币 CNY 管理");
            if (Context.Queryable<AiModelPrice>().Any(x => x.Id != input.Id && x.Provider == provider && x.Model == model))
                throw new CustomException("该 Provider 和模型的价格已存在");

            if (input.Id > 0)
            {
                var entity = GetPrice(input.Id) ?? throw new CustomException("模型价格不存在");
                entity.Provider = provider;
                entity.Model = model;
                entity.InputPricePerMillion = input.InputPricePerMillion;
                entity.OutputPricePerMillion = input.OutputPricePerMillion;
                entity.Currency = currency;
                entity.Status = input.Status;
                entity.Remark = input.Remark;
                entity.Update_by = App.UserName;
                entity.Update_time = DateTime.Now;
                Context.Updateable(entity).ExecuteCommand();
                return entity.Id;
            }

            var added = new AiModelPrice
            {
                Provider = provider,
                Model = model,
                InputPricePerMillion = input.InputPricePerMillion,
                OutputPricePerMillion = input.OutputPricePerMillion,
                Currency = currency,
                Status = input.Status,
                Remark = input.Remark,
                Create_by = App.UserName,
                Create_time = DateTime.Now
            };
            return Context.Insertable(added).ExecuteReturnIdentity();
        }

        public int DeletePrice(long id)
        {
            EnsurePlatformAdmin();
            return Context.Deleteable<AiModelPrice>().Where(x => x.Id == id).ExecuteCommand();
        }

        public AiGovernanceCapabilitiesDto GetCapabilities()
        {
            var platform = AiPermissionHelper.IsPlatformAdmin();
            return new AiGovernanceCapabilitiesDto
            {
                TenantId = App.GetCurrentTenantId(),
                IsPlatformAdmin = platform,
                CanManageGlobalPolicy = platform,
                CanManageModelPrice = platform,
                CanCheckProvider = platform
            };
        }

        public AiModelCatalogDto GetCatalog()
        {
            var map = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Add(string provider, string model, string label = null)
            {
                provider = (provider ?? string.Empty).Trim();
                model = (model ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(provider)) return;
                if (!map.TryGetValue(provider, out var models))
                {
                    models = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[provider] = models;
                }
                if (!string.IsNullOrEmpty(model)) models.Add(model);
                if (!string.IsNullOrWhiteSpace(label) && !labels.ContainsKey(provider))
                    labels[provider] = label.Trim();
            }

            Add(_options.Provider, _options.Model);
            var vision = AiLlmClient.ResolveVisionProvider(_options);
            Add(vision.Provider, vision.Model);
            if (_options.Providers != null)
            {
                foreach (var item in _options.Providers)
                {
                    Add(item?.Provider, item?.Model, item?.Label);
                    Add(item?.Provider, item?.VisionModel, item?.Label);
                    if (item?.Models == null) continue;
                    foreach (var model in item.Models) Add(item.Provider, model, item.Label);
                }
            }

            foreach (var row in Context.Queryable<AiModelPrice>()
                .GroupBy(x => new { x.Provider, x.Model })
                .Select(x => new { x.Provider, x.Model })
                .ToList())
            {
                Add(row.Provider, row.Model);
            }

            return new AiModelCatalogDto
            {
                Providers = [.. map.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new AiProviderOptionDto
                    {
                        Provider = x.Key.ToLowerInvariant(),
                        Label = labels.TryGetValue(x.Key, out var label) ? $"{label}（{x.Key}）" : x.Key,
                        Models = [.. x.Value]
                    })]
            };
        }

        public AiConfigCheckDto CheckConfiguration()
        {
            EnsurePlatformAdmin();
            var resolved = AiLlmClient.ResolveProvider(_options);
            var key = resolved.ApiKey ?? string.Empty;
            var vision = AiLlmClient.ResolveVisionProvider(_options);
            var result = new AiConfigCheckDto
            {
                Enabled = _options.Enable,
                Provider = resolved.Provider,
                BaseUrl = MaskUrl(resolved.BaseUrl),
                Endpoint = resolved.ChatEndpoint,
                Model = resolved.Model,
                ApiKeyConfigured = AiLlmClient.AllowsEmptyApiKey(resolved.Provider)
                    || (!string.IsNullOrWhiteSpace(key) && !key.Contains("xxx", StringComparison.OrdinalIgnoreCase)),
                VisionConfigured = !string.IsNullOrWhiteSpace(vision.Model),
                VisionModel = vision.Model
            };
            if (!_options.Enable) result.Warnings.Add("AI 总开关未启用");
            if (!Uri.TryCreate(resolved.BaseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                result.Warnings.Add("Provider BaseUrl 不是有效的 HTTP(S) 地址");
            else if (uri.Scheme != Uri.UriSchemeHttps && uri.Host is not "localhost" and not "127.0.0.1" and not "::1")
                result.Warnings.Add("Provider BaseUrl 未使用 HTTPS");
            if (!result.ApiKeyConfigured) result.Warnings.Add("API Key 缺失或仍为占位值");
            if (string.IsNullOrWhiteSpace(resolved.Model)) result.Warnings.Add("模型名称未配置");
            if (string.IsNullOrWhiteSpace(result.VisionModel))
            {
                result.Warnings.Add("当前 Provider 未配置 VisionModel，发图提问将失败");
            }
            if (_options.TimeoutSeconds <= 0) result.Warnings.Add("请求超时时间必须大于 0");
            var promptDir = string.IsNullOrWhiteSpace(_options.PromptDir)
                ? Path.Combine(AppContext.BaseDirectory, "Prompts")
                : _options.PromptDir;
            if (!Directory.Exists(promptDir))
            {
                result.Warnings.Add("Prompt 目录不存在");
            }
            else if (Directory.GetFiles(promptDir, "*.md", SearchOption.AllDirectories).Length == 0)
            {
                // 目录存在但为空同样是部署事故（发布时漏拷 Prompts），此时所有按文件加载提示词的能力都会报错
                result.Warnings.Add("Prompt 目录下没有任何 .md 提示词文件");
            }
            // 进程启动后实际读取失败的提示词（由 PromptLoader 记录）：让"AI 报缺提示词"在自检页可见，
            // 不再只留在日志里；文件名来自各调用点，无需在此维护第二份场景→提示词映射。
            var missingPrompts = PromptLoader.GetMissingFiles();
            if (missingPrompts.Count > 0)
            {
                var shown = string.Join("、", missingPrompts.Take(5));
                result.Warnings.Add($"以下提示词文件读取失败（对应 AI 能力调用时会直接报错）：{shown}"
                    + (missingPrompts.Count > 5 ? $" 等共 {missingPrompts.Count} 个" : string.Empty));
            }
            result.Valid = result.Warnings.Count == 0;
            return result;
        }

        public async Task<AiHealthCheckDto> CheckHealthAsync()
        {
            EnsurePlatformAdmin();
            var resolved = AiLlmClient.ResolveProvider(_options);
            var watch = Stopwatch.StartNew();
            try
            {
                var text = await AiLlmClient.ChatAsync(_options, "仅回复 OK", "ping", AiSceneCatalog.HealthCheck);
                var log = GetLatestHealthLog();
                return new AiHealthCheckDto
                {
                    Healthy = !string.IsNullOrWhiteSpace(text),
                    Provider = resolved.Provider,
                    Model = resolved.Model,
                    LatencyMs = watch.ElapsedMilliseconds,
                    HttpStatusCode = log?.HttpStatusCode,
                    ProviderRequestId = log?.ProviderRequestId,
                    Message = string.IsNullOrWhiteSpace(text) ? "Provider 返回空响应" : "连接正常",
                    CheckedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                var log = GetLatestHealthLog();
                return new AiHealthCheckDto
                {
                    Healthy = false,
                    Provider = resolved.Provider,
                    Model = resolved.Model,
                    LatencyMs = watch.ElapsedMilliseconds,
                    HttpStatusCode = log?.HttpStatusCode,
                    ProviderRequestId = log?.ProviderRequestId,
                    Message = SafeError(ex.Message),
                    CheckedAt = DateTime.Now
                };
            }
        }

        private AiCallLog GetLatestHealthLog()
        {
            var traceId = App.HttpContext?.TraceIdentifier;
            return Context.Queryable<AiCallLog>()
                .Where(x => x.Scene == AiSceneCatalog.HealthCheck && x.TraceId == traceId)
                .OrderBy(x => x.CreateTime, global::SqlSugar.OrderByType.Desc)
                .First();
        }

        private void NormalizePolicy(AiPolicySaveDto input)
        {
            input.ScopeType = (input.ScopeType ?? string.Empty).Trim().ToLowerInvariant();
            input.Scene = string.IsNullOrWhiteSpace(input.Scene) ? "*" : input.Scene.Trim().ToLowerInvariant();
            input.TenantId = (input.TenantId ?? string.Empty).Trim();
            if (input.ScopeType == "global")
            {
                input.TenantId = string.Empty;
                input.SubjectId = 0;
            }
            else if (input.ScopeType == "tenant")
            {
                input.SubjectId = 0;
            }
        }

        private void ValidatePolicy(AiPolicySaveDto input)
        {
            if (!ScopeTypes.Contains(input.ScopeType)) throw new CustomException("无效的策略作用域");
            if (!AiSceneCatalog.All.Contains(input.Scene)) throw new CustomException("无效的 AI 场景");
            if (input.IsEnabled is < 0 or > 1) throw new CustomException("启用状态只能为 0、1 或空");
            if (input.Status is < 0 or > 1) throw new CustomException("策略状态只能为 0 或 1");
            ValidatePositive(input.DailyTokenLimit, "日 Token");
            ValidatePositive(input.MonthlyTokenLimit, "月 Token");
            ValidatePositive(input.DailyAmountLimit, "日金额");
            ValidatePositive(input.MonthlyAmountLimit, "月金额");
            ValidatePositive(input.ConcurrentLimit, "并发");

            var currentTenant = App.GetCurrentTenantId();
            if (!AiPermissionHelper.IsPlatformAdmin())
            {
                if (input.ScopeType is "global" or "tenant") throw new CustomException("只有平台管理员可维护全局和租户策略");
                input.TenantId = currentTenant;
            }
            else if ((input.ScopeType is "role" or "user") && input.TenantId != currentTenant)
            {
                throw new CustomException("角色和用户策略须由目标租户管理员在对应租户上下文中维护");
            }
            if (input.ScopeType != "global" && string.IsNullOrWhiteSpace(input.TenantId))
                throw new CustomException("非全局策略必须指定租户");
            if ((input.ScopeType == "role" || input.ScopeType == "user") && input.SubjectId <= 0)
                throw new CustomException("角色或用户策略必须指定主体 ID");

            if (input.ScopeType == "role" && _roleService.SelectRoleById(input.SubjectId) == null)
                throw new CustomException("目标角色不存在");
        }

        /// <summary>
        /// 校验策略实体的租户归属（跨租户越权防护）。调用方必须已通过对应操作的权限校验，
        /// 本方法不再做"是否有治理权限"的判断——否则只读权限会被写操作顺带放行。
        /// </summary>
        private void AuthorizePolicy(AiAccessPolicy entity)
        {
            if (entity == null || AiPermissionHelper.IsPlatformAdmin()) return;
            if (entity.TenantId != App.GetCurrentTenantId() || entity.ScopeType is "global" or "tenant")
                throw new CustomException("无权访问该 AI 策略");
        }

        /// <summary>
        /// 查询策略实体。不做权限与租户判断，调用方须保证接口级权限已校验，并自行调用 AuthorizePolicy。
        /// </summary>
        private AiAccessPolicy FindPolicy(long id) => Queryable().First(x => x.Id == id);

        private List<AiPolicyListDto> FillPolicyNames(List<AiAccessPolicy> list)
        {
            var dtos = (list ?? new List<AiAccessPolicy>()).Select(ToListDto).ToList();
            if (dtos.Count == 0) return dtos;

            var tenantIds = dtos.Select(x => x.TenantId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            var tenantMap = tenantIds.Count == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : Context.Queryable<SysTenant>()
                    .Where(t => tenantIds.Contains(t.TenantId))
                    .ToList()
                    .GroupBy(t => t.TenantId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().TenantName, StringComparer.OrdinalIgnoreCase);

            var roleIds = dtos.Where(x => x.ScopeType == "role" && x.SubjectId > 0).Select(x => x.SubjectId).Distinct().ToList();
            var roleMap = roleIds.Count == 0
                ? new Dictionary<long, string>()
                : _roleService.Queryable().Where(r => roleIds.Contains(r.RoleId)).ToList()
                    .GroupBy(r => r.RoleId).ToDictionary(g => g.Key, g => g.First().RoleName);

            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.TenantId)) dto.TenantName = "全局";
                else if (tenantMap.TryGetValue(dto.TenantId, out var tenantName) && !string.IsNullOrWhiteSpace(tenantName))
                    dto.TenantName = tenantName;

                if (dto.ScopeType == "role" && roleMap.TryGetValue(dto.SubjectId, out var roleName))
                    dto.SubjectName = roleName;
            }
            return dtos;
        }

        private static AiPolicyListDto ToListDto(AiAccessPolicy source) => new()
        {
            Id = source.Id,
            ScopeType = source.ScopeType,
            TenantId = source.TenantId,
            SubjectId = source.SubjectId,
            Scene = source.Scene,
            IsEnabled = source.IsEnabled,
            DailyTokenLimit = source.DailyTokenLimit,
            MonthlyTokenLimit = source.MonthlyTokenLimit,
            DailyAmountLimit = source.DailyAmountLimit,
            MonthlyAmountLimit = source.MonthlyAmountLimit,
            ConcurrentLimit = source.ConcurrentLimit,
            Status = source.Status,
            Remark = source.Remark
        };

        private static void MapPolicy(AiPolicySaveDto source, AiAccessPolicy target)
        {
            target.ScopeType = source.ScopeType;
            target.TenantId = source.TenantId;
            target.SubjectId = source.SubjectId;
            target.Scene = source.Scene;
            target.IsEnabled = source.IsEnabled;
            target.DailyTokenLimit = source.DailyTokenLimit;
            target.MonthlyTokenLimit = source.MonthlyTokenLimit;
            target.DailyAmountLimit = source.DailyAmountLimit;
            target.MonthlyAmountLimit = source.MonthlyAmountLimit;
            target.ConcurrentLimit = source.ConcurrentLimit;
            target.Status = source.Status;
            target.Remark = source.Remark;
        }

        private static void ValidatePositive<T>(T? value, string name) where T : struct, IComparable<T>
        {
            if (value.HasValue && value.Value.CompareTo(default) < 0) throw new CustomException($"{name}额度不能小于 0");
        }

        private static void EnsurePlatformAdmin()
        {
            if (!AiPermissionHelper.IsPlatformAdmin()) throw new CustomException("只有平台管理员可执行该操作");
        }

        /// <summary>
        /// 权限码校验（管理员隐含放行，见 LoginUser.HasPermission）。
        /// 仅用于"操作类型由请求体决定、控制器按 URL 无法区分"的场景（当前只有 SavePolicy 的新增/修改分支）；
        /// 常规操作的类型与接口一一对应，权限码由 Controller 的 ActionPermissionFilter 校验，不在此重复，
        /// 避免两层各写一份导致口径漂移。
        /// </summary>
        private static void EnsurePermission(params string[] permissions)
        {
            var user = App.HttpContext?.GetCurrentUser();
            if (user == null || permissions == null || permissions.Length == 0
                || !permissions.Any(p => user.HasPermission(p)))
                throw new CustomException("当前账号没有 AI 治理管理权限");
        }

        private static string MaskUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value;
            return $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? string.Empty : ":" + uri.Port)}";
        }

        private static string SafeError(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "健康检查失败";
            var result = value.Replace("\r", " ").Replace("\n", " ");
            return result.Length <= 300 ? result : result[..300];
        }
    }
}
