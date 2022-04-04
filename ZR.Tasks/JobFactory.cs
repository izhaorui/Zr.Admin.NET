using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;
using System;

namespace ZR.Tasks
{
    public class JobFactory : IJobFactory
    {
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 注入反射获取依赖对象
        /// </summary>
        private readonly IServiceProvider _serviceProvider;
        public JobFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        /// <summary>
        /// 实现接口Job
        /// </summary>
        /// <param name="bundle"></param>
        /// <param name="scheduler"></param>
        /// <returns></returns>
        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            try
            {
                var serviceScope = _serviceProvider.CreateScope();
                var job = serviceScope.ServiceProvider.GetService(bundle.JobDetail.JobType) as IJob;
                return job;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                logger.Error(ex, ex.Message);
                throw;
            }
        }

        public void ReturnJob(IJob job)
        {
            if (job is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
