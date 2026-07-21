using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 日程管理service接口
    /// </summary>
    public interface IDailyScheduleService : IBaseService<DailySchedule>
    {
        /// <summary>
        /// 分页查询（按当前用户隔离）
        /// </summary>
        PagedInfo<DailySchedule> GetPages(DailyScheduleQueryDto parm, long userId);

        /// <summary>
        /// 查询详情（校验归属用户）
        /// </summary>
        DailySchedule GetById(long id, long userId);

        /// <summary>
        /// 新增（model 已由 Controller 设置 UserId 与审计字段）
        /// </summary>
        int AddDailySchedule(DailySchedule model);

        /// <summary>
        /// 更新（model 已由 Controller 设置 UserId 与审计字段）
        /// </summary>
        int UpdateDailySchedule(DailySchedule model);

        /// <summary>
        /// 删除（按当前用户隔离）
        /// </summary>
        int DeleteDailySchedule(long id, long userId);

        /// <summary>
        /// 切换完成状态（status: 0未完成 1已完成）
        /// </summary>
        int ChangeStatus(long id, string status, long userId);

        /// <summary>
        /// 统计（总/未完成/今日到期/已逾期，按当前用户隔离）
        /// </summary>
        DailyScheduleStatsDto GetStats(long userId);

        /// <summary>
        /// 未完成日程数（Status=0），用于消息中心日程 tab 红点。
        /// 在 SignalR 连接建立时触发，仅统计不写消息。
        /// </summary>
        int GetScheduleReminderCount(long userId);

        /// <summary>
        /// 未完成日程列表（Status=0），按 DueTime 升序、Priority 降序，供日程 tab 打开时查询。
        /// </summary>
        List<DailySchedule> GetReminderSchedules(long userId);
    }
}
