using ZR.Model;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// AI 工具调用审计：落库 + 治理页查询。
    /// <para>
    /// 写入方是对话编排（SysAiChatService 的工具执行收口点），不依赖任何业务 Service 的权限，
    /// 因此本服务只做"落库/查询"，自身不做权限判断。
    /// 权限边界与用量统计一致：接口级权限码由 AiGovernanceController 的 ActionPermissionFilter 负责，
    /// 本服务只按 isAdmin 决定数据范围——false 强制当前登录人，true 可看本租户或全平台（平台管理员）。
    /// </para>
    /// </summary>
    public interface IAiToolCallLogService : IBaseService<AiToolCallLog>
    {
        /// <summary>
        /// 批量写入工具调用审计（同一轮对话的多次工具调用一次落库）。
        /// 实现必须"失败不影响对话"：内部捕获异常并记 Warn 日志，返回实际写入行数。
        /// </summary>
        Task<int> RecordAsync(IReadOnlyList<AiToolCallLog> logs);

        /// <summary>
        /// 工具调用明细分页（创建时间倒序）
        /// </summary>
        PagedInfo<AiToolCallLogDto> GetList(AiToolCallQueryDto parm, bool isAdmin);
    }
}