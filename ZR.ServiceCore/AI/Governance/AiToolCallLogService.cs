using Infrastructure;
using Infrastructure.Attribute;
using NLog;
using System.Linq.Expressions;
using ZR.Model;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.Repository;
using ZR.ServiceCore.AI.IService;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 工具调用审计（ai_tool_call_log）：对话编排写入 + 治理页查询。
    /// <para>
    /// 写入是"旁路审计"：失败只记 Warn 日志、绝不影响本轮对话（工具结果已经拿到，
    /// 不能因为审计落库失败就把用户可见的回答也弄挂）。因此这里独立 try/catch，不向上抛。
    /// </para>
    /// <para>
    /// 查询的数据范围与 ai_call_log 用量页口径一致（见 SysAiUsageService.BuildWhere）：
    /// 非管理员强制当前登录人 + 当前租户；租户管理员看本租户；平台管理员可按租户筛全平台。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IAiToolCallLogService), ServiceLifetime = LifeTime.Transient)]
    public class AiToolCallLogService : BaseService<AiToolCallLog>, IAiToolCallLogService
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>默认统计窗口天数（工具调用量远大于模型调用，默认窗口收窄到 7 天）</summary>
        private const int DefaultDays = 7;

        public async Task<int> RecordAsync(IReadOnlyList<AiToolCallLog> logs)
        {
            if (logs == null || logs.Count == 0) return 0;

            try
            {
                var rows = logs as IList<AiToolCallLog> ?? logs.ToList();
                // Id 非自增（雪花）：批量插入需调用方未指定时由这里补齐，避免多行主键冲突
                foreach (var row in rows)
                {
                    if (row.Id <= 0) row.Id = SnowFlakeSingle.Instance.NextId();
                }
                return await Context.Insertable(rows.ToList()).ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                // 审计表未建/库不可用时不能阻断对话，只留下可搜索的告警
                _logger.Warn($"AI 工具调用审计写入失败 count={logs.Count} tool={string.Join(",", logs.Select(x => x.ToolName).Distinct())} err={ex.Message}");
                return 0;
            }
        }

        public PagedInfo<AiToolCallLogDto> GetList(AiToolCallQueryDto parm, bool isAdmin)
        {
            parm ??= new AiToolCallQueryDto();
            var (begin, end) = ResolveRange(parm);
            var page = GetPages(BuildWhere(parm, isAdmin, begin, end), parm, m => m.CreateTime, OrderByType.Desc);

            return new PagedInfo<AiToolCallLogDto>
            {
                PageIndex = page.PageIndex,
                PageSize = page.PageSize,
                TotalNum = page.TotalNum,
                Result = page.Result?.Select(MapLog).ToList()
            };
        }

        private static AiToolCallLogDto MapLog(AiToolCallLog m) => new()
        {
            CreateTime = m.CreateTime,
            UserName = string.IsNullOrWhiteSpace(m.UserName)
                ? (m.UserId <= 0 ? "系统任务" : $"用户{m.UserId}")
                : m.UserName,
            TenantId = m.TenantId,
            ToolName = m.ToolName,
            Provider = m.Provider,
            Permission = m.Permission,
            Status = m.Status,
            DurationMs = m.DurationMs,
            ArgsSummary = m.ArgsSummary,
            ErrorMsg = m.ErrorMsg,
            TraceId = m.TraceId,
            SessionId = m.SessionId
        };

        private static (DateTime Begin, DateTime End) ResolveRange(AiToolCallQueryDto parm)
        {
            var end = parm.EndTime ?? DateTime.Now;
            var begin = parm.BeginTime ?? end.Date.AddDays(-DefaultDays);
            return (begin, end);
        }

        /// <summary>
        /// 组装查询条件：个人视角强制当前登录人（不信任调用方传入的 UserId）；
        /// 租户管理员收窄到当前租户；平台管理员可跨租户筛选。
        /// </summary>
        private Expression<Func<AiToolCallLog, bool>> BuildWhere(
            AiToolCallQueryDto parm, bool isAdmin, DateTime begin, DateTime end)
        {
            var exp = Expressionable.Create<AiToolCallLog>();
            exp = exp.And(m => m.CreateTime >= begin && m.CreateTime <= end);

            if (!isAdmin)
            {
                exp = exp.And(m => m.TenantId == App.GetCurrentTenantId()
                    && m.UserId == DataScopeExtensions.GetCurrentUserId());
            }
            else if (!AiPermissionHelper.IsPlatformAdmin())
            {
                exp = exp.And(m => m.TenantId == App.GetCurrentTenantId());
                if (parm.UserId.HasValue) exp = exp.And(m => m.UserId == parm.UserId.Value);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(parm.TenantId)) exp = exp.And(m => m.TenantId == parm.TenantId);
                if (parm.UserId.HasValue) exp = exp.And(m => m.UserId == parm.UserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(parm.UserName)) exp = exp.And(m => m.UserName.Contains(parm.UserName.Trim()));
            if (!string.IsNullOrWhiteSpace(parm.ToolName)) exp = exp.And(m => m.ToolName == parm.ToolName.Trim());
            if (!string.IsNullOrWhiteSpace(parm.Provider)) exp = exp.And(m => m.Provider == parm.Provider.Trim());
            if (!string.IsNullOrWhiteSpace(parm.Status)) exp = exp.And(m => m.Status == parm.Status.Trim());
            if (!string.IsNullOrWhiteSpace(parm.Permission)) exp = exp.And(m => m.Permission == parm.Permission.Trim());
            if (!string.IsNullOrWhiteSpace(parm.TraceId)) exp = exp.And(m => m.TraceId == parm.TraceId.Trim());
            if (parm.SessionId.HasValue) exp = exp.And(m => m.SessionId == parm.SessionId.Value);
            return exp.ToExpression();
        }
    }
}