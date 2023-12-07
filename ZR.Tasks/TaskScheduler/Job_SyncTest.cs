using Infrastructure.Attribute;
using Quartz;
using SqlSugar.IOC;
using System.Threading.Tasks;
using ZR.Model.System;

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
            await ExecuteJob(context, Run);            
        }

        /// <summary>
        /// 任务使用中注意：所有方法都需要使用异步，并且不能少了await
        /// </summary>
        /// <returns></returns>
        public async Task Run()
        {
            await Task.Delay(1);
            //TODO 业务逻辑
            var db = DbScoped.SugarScope;
            var info = await db.Queryable<SysDept>().FirstAsync();

            //其他库操作
            //var db2 = DbScoped.SugarScope.GetConnectionScope(2);
            System.Console.WriteLine("job test");
        }
    }
}
