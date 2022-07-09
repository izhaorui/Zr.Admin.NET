using Infrastructure;
using Infrastructure.Attribute;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;
using System;
using System.Threading.Tasks;
using ZR.Service.System.IService;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 定时任务http请求
    /// </summary>
    [AppService(ServiceType = typeof(Job_HttpRequest), ServiceLifetime = LifeTime.Scoped)]
    internal class Job_HttpRequest : JobBase, IJob
    {
        private readonly ISysTasksQzService tasksQzService;
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Job_HttpRequest(ISysTasksQzService tasksQzService)
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
            if (info != null)
            {
                var result = await HttpHelper.HttpGetAsync("http://" + info.ApiUrl);
                logger.Info($"任务【{info.Name}】网络请求执行结果=" + result);
            }
            else
            {
                throw new CustomException($"任务{trigger?.JobName}网络请求执行失败，任务不存在");
            }
        }
    }
}
