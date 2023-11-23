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

        public async Task<SysTasksLog> AddTaskLog(string jobId, SysTasksLog logModel)
        {
            //获取任务信息
            var model = await _tasksQzService.GetSingleAsync(f => f.ID == jobId);

            if (model != null)
            {
                logModel.JobId = jobId;
                logModel.JobName = model.Name;
                logModel.JobGroup = model.JobGroup;
                logModel.CreateTime = DateTime.Now;
            }

            await InsertAsync(logModel);
            return logModel;
        }

    }
}
