using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;
using System;
using System.Runtime.CompilerServices;

namespace ZR.Tasks
{
    public class JobFactory : IJobFactory
    {
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 注入反射获取依赖对象
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 记录每个 Job 实例对应的 DI 作用域，ReturnJob 时释放。
        /// 用 ConditionalWeakTable 关联，避免实例被长期强引用导致泄漏。
        /// </summary>
        private readonly ConditionalWeakTable<IJob, IServiceScope> _jobScopes = new();

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
            var jobType = bundle.JobDetail.JobType;
            try
            {
                var serviceScope = _serviceProvider.CreateScope();
                var job = serviceScope.ServiceProvider.GetService(jobType) as IJob;
                if (job == null)
                {
                    // 解析失败时必须释放刚创建的作用域，避免泄漏，并抛出可定位的异常
                    serviceScope.Dispose();
                    throw new InvalidOperationException(
                        $"无法从 DI 容器解析任务类型: {jobType.FullName}，请确认已用 [AppService] 注册或加入服务容器。");
                }

                _jobScopes.Add(job, serviceScope);
                return job;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"创建任务实例失败: {jobType.FullName}");
                throw;
            }
        }

        public void ReturnJob(IJob job)
        {
            if (job == null)
            {
                return;
            }

            // 优先释放 Job 所属的 DI 作用域（Scoped 依赖随之释放）；
            // 若该实例不是通过本工厂创建（无对应作用域），则回退到释放 Job 自身。
            if (_jobScopes.TryGetValue(job, out var scope))
            {
                _jobScopes.Remove(job);
                scope.Dispose();
                return;
            }

            if (job is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
