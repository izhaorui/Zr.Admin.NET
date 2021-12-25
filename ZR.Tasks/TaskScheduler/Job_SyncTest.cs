using Quartz;
using System.Threading.Tasks;

namespace ZR.Tasks
{
    /// <summary>
    /// 定时任务测试
    /// </summary>
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

        }
    }
}
