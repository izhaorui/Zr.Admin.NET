using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;
using SqlSugar.IOC;
using System.Threading.Tasks;
using ZR.ServiceCore.Services;

namespace ZR.Tasks.TaskScheduler
{
    [AppService(ServiceType = typeof(Job_SqlExecute), ServiceLifetime = LifeTime.Scoped)]
    public class Job_SqlExecute : JobBase, IJob
    {
        private readonly ISysTasksQzService tasksQzService;
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Job_SqlExecute(ISysTasksQzService tasksQzService)
        {
            this.tasksQzService = tasksQzService;
        }
        public async Task Execute(IJobExecutionContext context)
        {
            await ExecuteJob(context, async () => await Run(context));
        }
        public async Task Run(IJobExecutionContext context)
        {
            AbstractTrigger trigger = (context as JobExecutionContextImpl).Trigger as AbstractTrigger;
            
            var info = await tasksQzService.GetByIdAsync(trigger.JobName);

            if (info != null && info.SqlText.IsNotEmpty())
            {
                var result = DbScoped.SugarScope.Ado.ExecuteCommandWithGo(info.SqlText);
                logger.Info($"任务【{info.Name}】sql请求执行结果=" + result);
            }
            else
            {
                throw new CustomException($"任务{trigger?.JobName}执行失败，任务不存在");
            }
        }
    }
}
