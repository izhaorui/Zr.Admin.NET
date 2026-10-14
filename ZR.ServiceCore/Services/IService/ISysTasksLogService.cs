using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    public interface ISysTasksLogService : IBaseService<SysTasksLog>
    {
        /// <summary>
        /// 记录任务执行日志
        /// </summary>
        /// <param name="jobId">任务ID</param>
        /// <param name="tasksLog">日志内容</param>
        /// <param name="taskInfo">任务信息；为 null 时内部按 jobId 查询（调用方已持有任务时传入可避免重复查询）</param>
        /// <returns></returns>
        Task<SysTasksLog> AddTaskLog(string jobId, SysTasksLog tasksLog, SysTasks taskInfo = null);
    }
}
