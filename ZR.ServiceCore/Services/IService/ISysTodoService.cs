using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 个人待办service接口
    /// </summary>
    public interface ISysTodoService : IBaseService<SysTodo>
    {
        /// <summary>
        /// 分页查询（按当前用户隔离）
        /// </summary>
        PagedInfo<SysTodo> GetPages(SysTodoQueryDto parm, long userId);

        /// <summary>
        /// 查询详情（校验归属用户）
        /// </summary>
        SysTodo GetById(long id, long userId);

        /// <summary>
        /// 新增（model 已由 Controller 设置 UserId 与审计字段）
        /// </summary>
        int AddSysTodo(SysTodo model);

        /// <summary>
        /// 更新（model 已由 Controller 设置 UserId 与审计字段）
        /// </summary>
        int UpdateSysTodo(SysTodo model);

        /// <summary>
        /// 删除（按当前用户隔离）
        /// </summary>
        int DeleteSysTodo(long id, long userId);

        /// <summary>
        /// 切换完成状态（status: 0未完成 1已完成）
        /// </summary>
        int ChangeStatus(long id, string status, long userId);

        /// <summary>
        /// 统计（总/未完成/今日到期/已逾期，按当前用户隔离）
        /// </summary>
        SysTodoStatsDto GetStats(long userId);

        /// <summary>
        /// 未完成待办数（Status=0），用于消息中心待办 tab 红点。
        /// 在 SignalR 连接建立时触发，仅统计不写消息。
        /// </summary>
        int GetTodoReminderCount(long userId);

        /// <summary>
        /// 未完成待办列表（Status=0），按 DueTime 升序、Priority 降序，供待办 tab 打开时查询。
        /// </summary>
        List<SysTodo> GetReminderTodos(long userId);
    }
}
