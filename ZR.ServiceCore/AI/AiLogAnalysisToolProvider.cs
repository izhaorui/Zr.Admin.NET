using Infrastructure.AI;
using Infrastructure.Attribute;
using Newtonsoft.Json.Linq;
using NLog;
using System.Text;
using ZR.Model.AI.Dto;
using ZR.Model.System.Dto;
using ZR.ServiceCore.AI.IService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.AI
{
    /// <summary>
    /// AI 助手扩展工具：日志分析（登录安全 / 操作日志健康）。
    /// 复用登录/操作日志页面 AI 分析同一套聚合指标服务，指标在服务端聚合、IP 按权限脱敏，
    /// 工具只把聚合指标文本回灌给模型解读，不接触原始日志、不生成 SQL；
    /// 权限口径与页面一致（monitor:logininfor:ai / monitor:operlog:ai，管理员隐含放行）。
    /// </summary>
    [AppService(ServiceType = typeof(IAiAssistantToolProvider), ServiceLifetime = LifeTime.Transient)]
    public class AiLogAnalysisToolProvider : IAiAssistantToolProvider
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        /// <summary>操作日志 AI 分析页面权限码</summary>
        private const string OperAiPerm = "monitor:operlog:ai";
        /// <summary>登录日志 AI 分析页面权限码</summary>
        private const string LoginAiPerm = "monitor:logininfor:ai";
        /// <summary>趋势明细最多输出天数，超出折叠避免回灌文本过长</summary>
        private const int MaxTrendLines = 14;

        private readonly ISysLoginService _loginService;
        private readonly ISysOperLogService _operLogService;
        private readonly ISysUserService _userService;
        private readonly ISysPermissionService _permissionService;

        public string ProviderName => "log-analysis";

        public AiLogAnalysisToolProvider(
            ISysLoginService loginService,
            ISysOperLogService operLogService,
            ISysUserService userService,
            ISysPermissionService permissionService)
        {
            _loginService = loginService;
            _operLogService = operLogService;
            _userService = userService;
            _permissionService = permissionService;
        }

        public List<AiToolDef> GetToolDefs()
        {
            return new List<AiToolDef>
            {
                new AiToolDef
                {
                    Name = "analyze_login_security",
                    Label = "登录安全分析",
                    // 与页面 AI 分析同一权限口径：无权限用户不展示该工具，避免能力名外泄
                    Permission = LoginAiPerm,
                    Samples = new List<string>
                    {
                        "最近有没有异常登录？",
                        "帮我分析一下最近 7 天的登录安全",
                        "把异地登录的账号都列出来"
                    },
                    Description = "分析最近 N 天系统登录日志的安全聚合指标（登录总量/成功失败与失败率、凌晨异常时段登录、登录失败账号与失败来源 IP Top、期间新出现的 IP、异地/新地点成功登录账号（同账号多地点成功登录、相对近30天成功历史出现新地点、2小时内跨地点切换）、浏览器/操作系统分布）。可用 limit 放大异地账号返回条数以列全（账号为系统登录名，LLM 无需也无法提前预知，仅按名单逐条向用户确认后配合 limit 全量输出）。数据由服务端预聚合并按要求对 IP 脱敏，不返回原始日志。适用于“分析登录日志/登录安全/有没有异常登录/异地登录/把异地登录的账号查出来/谁在爆破/失败登录多”等提问。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["days"] = new { type = "integer", description = "统计天数，默认 7，最大 90" },
                            ["limit"] = new { type = "integer", description = "异地/新地点成功登录账号返回条数上限，默认 15，最大 50；提问要求把异地登录账号全部列出来时可传 50" }
                        },
                        required = Array.Empty<string>()
                    }
                },
                new AiToolDef
                {
                    Name = "analyze_oper_health",
                    Label = "操作健康分析",
                    Permission = OperAiPerm,
                    Samples = new List<string>
                    {
                        "最近系统操作日志健康吗？",
                        "有哪些操作报错比较多？"
                    },
                    Description = "分析最近 N 天操作日志的健康聚合指标（操作总量/错误量与错误率、耗时 P95、慢操作 Top、业务类型分布、删除/导出/强退/清空等敏感操作 Top 账号、服务端归一化后的错误聚类）。管理员统计全量，非管理员只统计其本人操作日志。适用于“系统操作日志健康吗/最近有没有报错/哪个功能慢/谁在大量删除导出”等提问。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["days"] = new { type = "integer", description = "统计天数，默认 7，最大 90" }
                        },
                        required = Array.Empty<string>()
                    }
                }
            };
        }

        public async Task<AiToolExecResult> ExecuteAsync(string toolName, string argsJson, long userId)
        {
            switch (toolName)
            {
                case "analyze_login_security":
                    return await AnalyzeLoginSecurityAsync(argsJson, userId);
                case "analyze_oper_health":
                    return await AnalyzeOperHealthAsync(argsJson, userId);
                default:
                    return null;
            }
        }

        #region 工具实现

        /// <summary>
        /// 登录安全指标：权限与页面 aiSecurity 一致（monitor:logininfor:ai）。
        /// 数据范围/租户库/IP 脱敏由 GetLoginSecurityMetrics 内部处理，工具不额外展开范围。
        /// </summary>
        private Task<AiToolExecResult> AnalyzeLoginSecurityAsync(string argsJson, long userId)
        {
            var perms = AiPermissionHelper.TryLoadPerms(_permissionService, userId);
            if (perms == null)
            {
                // 权限计算失败（≠"无权限"）：只说可重试，避免误导用户去申请授权
                return Task.FromResult(AiToolExecResult.Error("暂时无法校验权限，请稍后重试。"));
            }
            if (!AiPermissionHelper.HasPerm(perms, LoginAiPerm))
            {
                // 不回显权限码：无权限时该工具已不在下发模型的工具数组与前端清单中，
                // 走到这里通常是被绕过校验的异常调用，错误文案不应再透露权限编码
                return Task.FromResult(AiToolExecResult.Error("你没有分析登录日志的权限，如需使用请联系管理员授权。"));
            }

            var input = ParseInput(argsJson);
            var metrics = _loginService.GetLoginSecurityMetrics(input);
            if (metrics == null || metrics.TotalCount <= 0)
            {
                var (b, e) = input.ResolveRange();
                return Task.FromResult(AiToolExecResult.Success($"在 {b:yyyy-MM-dd} 至 {e:yyyy-MM-dd} 期间没有登录日志，无需分析。"));
            }

            return Task.FromResult(AiToolExecResult.Success(FormatLoginMetrics(metrics)));
        }

        /// <summary>
        /// 操作日志健康指标：权限与页面 aiHealth 一致（monitor:operlog:ai）。
        /// 管理员全量，非管理员仅统计本人（按 UserId 过滤，不再依赖用户名），与页面同口径。
        /// </summary>
        private Task<AiToolExecResult> AnalyzeOperHealthAsync(string argsJson, long userId)
        {
            var perms = AiPermissionHelper.TryLoadPerms(_permissionService, userId);
            if (perms == null)
            {
                return Task.FromResult(AiToolExecResult.Error("暂时无法校验权限，请稍后重试。"));
            }
            if (!AiPermissionHelper.HasPerm(perms, OperAiPerm))
            {
                return Task.FromResult(AiToolExecResult.Error("你没有分析操作日志的权限，如需使用请联系管理员授权。"));
            }

            var isAdmin = AiPermissionHelper.IsAdminPerms(perms);
            var input = ParseInput(argsJson);
            long? scopeUserId = null;
            var owner = "全部用户";
            if (!isAdmin)
            {
                scopeUserId = userId;
                var operName = _userService.SelectUserById(userId)?.UserName;
                owner = string.IsNullOrWhiteSpace(operName) ? "当前用户" : $"用户 {operName}";
            }

            var metrics = _operLogService.GetOperHealthMetrics(input, scopeUserId);
            if (metrics == null || metrics.TotalCount <= 0)
            {
                var (b, e) = input.ResolveRange();
                return Task.FromResult(AiToolExecResult.Success($"在 {b:yyyy-MM-dd} 至 {e:yyyy-MM-dd} 期间（{owner}）没有操作日志，无需分析。"));
            }

            return Task.FromResult(AiToolExecResult.Success(FormatOperMetrics(metrics, owner)));
        }

        /// <summary>
        /// 解析 days / limit 参数构造输入；未传或非法时走默认（近 7 天 / Top15）。
        /// days 上限 90 截断；limit 上限 50 截断（最终由服务端 clamp 1-50）。
        /// </summary>
        private static LogAiAnalysisInput ParseInput(string argsJson)
        {
            int days = 7;
            int? limit = null;
            if (!string.IsNullOrWhiteSpace(argsJson))
            {
                try
                {
                    var args = JObject.Parse(argsJson);
                    days = args["days"]?.Value<int>() ?? 7;
                    if (args["limit"] != null)
                    {
                        limit = args["limit"].Value<int>();
                    }
                }
                catch
                {
                    days = 7;
                }
            }
            days = Math.Clamp(days, 1, 90);
            return new LogAiAnalysisInput { BeginTime = DateTime.Now.Date.AddDays(1 - days), EndTime = null, Limit = limit };
        }

        #endregion 工具实现

        #region 指标格式化

        private static string FormatLoginMetrics(LoginSecurityMetricsDto m)
        {
            var failRate = m.TotalCount > 0 ? m.FailCount * 100.0 / m.TotalCount : 0;
            var lines = new List<string>
            {
                $"登录安全分析（{m.TimeRange}）",
                $"登录总数 {m.TotalCount}：成功 {m.SuccessCount}，失败 {m.FailCount}（失败率 {failRate:0.0}%）",
                $"凌晨0-6点登录 {m.NightCount} 次，其中失败 {m.NightFailCount} 次",
                $"区间内去重 IP {m.DistinctIpCount} 个，其中近30天未出现的新 IP {m.NewIpCount} 个{(m.IpMasked ? "（当前无查看真实IP权限，IP已脱敏）" : "")}",
                "异地/新地点成功登录：" + FormatRemote(m),
                "每日趋势：" + FormatDaily(m.Daily, d => $"{d.Date} 成{d.Success}/败{d.Fail}"),
                "登录失败账号 Top：" + FormatTop(m.FailedAccounts, a =>
                    $"{a.UserName} {a.FailCount}次/来自{a.LocationCount}地{(string.IsNullOrEmpty(a.Locations) ? "" : $"({a.Locations})")}"),
                "登录失败来源 IP Top：" + FormatTop(m.FailedIps, a =>
                    $"{a.Ipaddr} {a.FailCount}次{(string.IsNullOrEmpty(a.Location) ? "" : $"/{a.Location}")}"),
                "浏览器分布：" + FormatTop(m.Browsers, a => $"{a.Name} {a.Count}"),
                "操作系统分布：" + FormatTop(m.Oses, a => $"{a.Name} {a.Count}")
            };

            return string.Join("\n", lines);
        }

        private static string FormatOperMetrics(OperHealthMetricsDto m, string owner)
        {
            var errRate = m.TotalCount > 0 ? m.ErrorCount * 100.0 / m.TotalCount : 0;
            var p95 = m.P95Elapsed.HasValue ? $"{m.P95Elapsed.Value:0} ms" : "未计算（样本过大或缺失）";
            var lines = new List<string>
            {
                $"操作日志健康分析（{m.TimeRange}，范围：{owner}）",
                $"操作总数 {m.TotalCount}：错误 {m.ErrorCount}（错误率 {errRate:0.00}%）",
                $"耗时 P95：{p95}；凌晨0-6点操作 {m.NightCount} 次",
                "每日趋势：" + FormatDaily(m.Daily, d => $"{d.Date} 共{d.Total}/错{d.Errors}"),
                "业务类型分布：" + FormatTop(m.BusinessTypes, t =>
                    $"{t.TypeName} {t.Total}次/错{t.Errors}"),
                "慢操作 Top：" + FormatTop(m.SlowOps, s =>
                    $"{s.Title}({s.Method}) 均{s.AvgElapsed:0}ms/最大{s.MaxElapsed}ms ×{s.Count}次"),
                "敏感操作(删除/导出/强退/清空)Top 账号：" + FormatTop(m.RiskAccounts, a => $"{a.Name} {a.Count}次"),
                "错误聚类 Top：" + FormatTop(m.ErrorClusters, c =>
                    $"[{c.Count}次] {AiHelper.ClipText(c.Pattern, 80)}（示例：{AiHelper.ClipText(c.SampleErrorMsg ?? "", 80)} / {c.SampleTitle}）")
            };
            if (!string.IsNullOrWhiteSpace(m.SampleNote))
            {
                lines.Add($"说明：{m.SampleNote}");
            }

            return string.Join("\n", lines);
        }

        /// <summary>异地/新地点成功登录汇总与 Top 明细；无异常输出"未发现"。</summary>
        private static string FormatRemote(LoginSecurityMetricsDto m)
        {
            if (m.RemoteLoginAccounts.Count == 0)
            {
                return "未发现异地/新地点成功登录的账号";
            }

            var items = m.RemoteLoginAccounts.Select(a =>
            {
                var sb = new StringBuilder($"{a.UserName} 成功{a.SuccessCount}次/来源{a.LocationCount}地({a.Locations})");
                if (a.NewLocationCount > 0)
                {
                    sb.Append($"，新增地点{a.NewLocationCount}个({a.NewLocations})");
                }
                if (a.RapidSwitchCount > 0)
                {
                    sb.Append($"，2h内跨地{a.RapidSwitchCount}次");
                }
                if (a.LastLoginTime.HasValue)
                {
                    sb.Append($"（最近 {a.LastLoginTime.Value:MM-dd HH:mm}）");
                }
                return sb.ToString();
            });
            var head = m.RemoteLoginAccountCount > m.RemoteLoginAccounts.Count
                ? $"共{m.RemoteLoginAccountCount}个账号，列 Top{m.RemoteLoginAccounts.Count}："
                : $"共{m.RemoteLoginAccountCount}个账号：";
            return head + string.Join("；", items);
        }

        /// <summary>每日趋势：超过上限只列最近 MaxTrendLines 天，避免回灌文本超长。</summary>
        private static string FormatDaily<T>(List<T> daily, Func<T, string> fmt)
        {
            if (daily == null || daily.Count == 0) return "无";
            var rows = daily.Skip(Math.Max(0, daily.Count - MaxTrendLines)).Select(fmt).ToList();
            var head = daily.Count > MaxTrendLines ? $"(共{daily.Count}天，列最近{MaxTrendLines}天) " : "";
            return head + string.Join("；", rows);
        }

        private static string FormatTop<T>(List<T> list, Func<T, string> fmt)
        {
            if (list == null || list.Count == 0) return "无";
            return string.Join("；", list.Select(x => fmt(x)));
        }

        #endregion 指标格式化
    }
}
