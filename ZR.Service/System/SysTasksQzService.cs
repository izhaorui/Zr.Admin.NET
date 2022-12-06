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
    public class SysTasksQzService : BaseService<SysTasks>, ISysTasksQzService
    {
        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int AddTasks(SysTasks parm)
        {
            parm.IsStart = false;

            SetAssembleName(parm);

            return Add(parm);
        }

        private void SetAssembleName(SysTasks parm)
        {
            if (parm.ApiUrl.IfNotEmpty() && parm.TaskType == 2)
            {
                parm.AssemblyName = "ZR.Tasks";
                parm.ClassName = "TaskScheduler.Job_HttpRequest";
            }

            if (parm.SqlText.IfNotEmpty() && parm.TaskType == 3)
            {
                parm.AssemblyName = "ZR.Tasks";
                parm.ClassName = "TaskScheduler.Job_SqlExecute";
            }
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int UpdateTasks(SysTasks parm)
        {
            SetAssembleName(parm);

            return Update(f => f.ID == parm.ID, f => new SysTasks
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
                SqlText = parm.SqlText,
                RequestMethod = parm.RequestMethod,
            });
        }
    }
}
