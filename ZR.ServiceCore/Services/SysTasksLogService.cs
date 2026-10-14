using Infrastructure.Attribute;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 任务日志
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient, ServiceType = typeof(ISysTasksLogService))]
    public class SysTasksLogService : BaseService<SysTasksLog>, ISysTasksLogService
    {
        private ISysTasksQzService _tasksQzService;
        public SysTasksLogService(ISysTasksQzService tasksQzService)
        {
            _tasksQzService = tasksQzService;
        }

        public async Task<SysTasksLog> AddTaskLog(string jobId, SysTasksLog logModel, SysTasks taskInfo = null)
        {
            logModel.JobId = jobId;

            // 已传入任务信息时直接使用，避免分发器按租户展开时每个租户都重复查一次任务表
            var model = taskInfo ?? await _tasksQzService.GetSingleAsync(f => f.ID == jobId);
            if (model != null)
            {
                logModel.JobName = model.Name;
                logModel.JobGroup = model.JobGroup;
            }

            logModel.CreateTime = DateTime.Now;
            await InsertAsync(logModel);
            return logModel;
        }

    }
}
