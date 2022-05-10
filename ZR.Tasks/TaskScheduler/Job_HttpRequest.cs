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
            var info = await tasksQzService.GetByIdAsync(trigger.Name);
            if (info != null)
            {
                var result = await HttpHelper.HttpGetAsync("http://" + info.ApiUrl);
                Console.WriteLine(result);
            }
        }
    }
}
