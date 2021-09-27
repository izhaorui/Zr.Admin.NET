using Infrastructure.Attribute;
using System;
using ZR.Model.System;
using ZR.Repository;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    [AppService(ServiceLifetime = LifeTime.Transient, ServiceType = typeof(ISysTasksLogService))]
    public class SysTasksLogService : BaseRepository<SysTasksLog>, ISysTasksLogService
    {
        private ISysTasksQzService _tasksQzService;
        public SysTasksLogService(ISysTasksQzService tasksQzService)
        {
            _tasksQzService = tasksQzService;
        }

        public SysTasksLog AddTaskLog(string jobId, SysTasksLog logModel)
        {
            //获取任务信息
            var model = _tasksQzService.GetId(jobId);

            if (model != null)
            {
                logModel.JobId = jobId;
                logModel.JobName = model.Name;
                logModel.JobGroup = model.JobGroup;
                logModel.CreateTime = DateTime.Now;
            }

            Insert(logModel, true);
            return logModel;
        }
    }
}
