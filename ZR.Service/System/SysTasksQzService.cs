using Infrastructure.Attribute;
using Infrastructure.Extensions;
using System;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 定时任务
    /// </summary>
    [AppService(ServiceType = typeof(ISysTasksQzService), ServiceLifetime = LifeTime.Transient)]
    public class SysTasksQzService : BaseService<SysTasksQz>, ISysTasksQzService
    {
        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int AddTasks(SysTasksQz parm)
        {
            parm.IsStart = false;

            if (parm.ApiUrl.IfNotEmpty() && parm.TaskType == 2)
            {
                parm.AssemblyName = "ZR.Tasks";
                parm.ClassName = "TaskScheduler.Job_HttpRequest";
            }
            return Add(parm);
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int UpdateTasks(SysTasksQz parm)
        {
            return Update(f => f.ID == parm.ID, f => new SysTasksQz
            {
                ID = parm.ID,
                Name = parm.Name,
                JobGroup = parm.JobGroup,
                Cron = parm.Cron,
                AssemblyName = parm.AssemblyName,
                ClassName = parm.ClassName,
                Remark = parm.Remark,
                TriggerType = parm.TriggerType,
                IntervalSecond = parm.IntervalSecond,
                JobParams = parm.JobParams,
                Update_time = DateTime.Now,
                BeginTime = parm.BeginTime,
                EndTime = parm.EndTime,
                TaskType = parm.TaskType,
                ApiUrl = parm.ApiUrl,
            });
        }
    }
}
