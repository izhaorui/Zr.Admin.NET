using Infrastructure.Attribute;
using Quartz;
using System.Threading.Tasks;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 定时任务测试
    /// 使用如下注册后TaskExtensions里面不用再注册了
    /// </summary>
    [AppService(ServiceType = typeof(Job_SyncTest), ServiceLifetime = LifeTime.Scoped)]
    public class Job_SyncTest : JobBase, IJob
    {
        //private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public async Task Execute(IJobExecutionContext context)
        {
            await ExecuteJob(context, async () => await Run());            
        }

        public async Task Run()
        {
            await Task.Delay(1);
            //TODO 业务逻辑

            System.Console.WriteLine("job test");
        }
    }
}
