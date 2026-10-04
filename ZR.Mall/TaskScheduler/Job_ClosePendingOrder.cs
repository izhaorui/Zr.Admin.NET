using Quartz;
using ZR.Mall.Service.IService;

namespace ZR.Mall.TaskScheduler
{
    /// <summary>
    /// 商城定时任务：关闭超时未支付的待付款订单并回补库存。
    /// 由系统任务调度触发（sys_tasks：AssemblyName=ZR.Mall，ClassName=Job_ClosePendingOrder，默认每5分钟）。
    /// SaaS 下经 Job_Dispatcher 按租户展开（TenantId="*"），在当前租户库关单；
    /// 非 SaaS 仍靠订单实体 [Tenant("MallDb")] 路由到商城库。
    /// </summary>
    public class Job_ClosePendingOrder : IJob
    {
        /// <summary>
        /// 技术日志（NLog → logs/all.log）。
        /// 原先用 Console.WriteLine：容器化/服务化部署后控制台输出不落盘，任务跑了什么、跑了几单全查不到。
        /// </summary>
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var svc = (IOMSOrderStateService)App.GetRequiredService(typeof(IOMSOrderStateService));
                var closed = svc.CloseExpiredPendingOrders(30); // 30 分钟未支付自动取消
                logger.Info($"定时任务：已自动取消 {closed} 笔超时待付款订单（30 分钟未支付）");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // 交给调度器记录失败日志
                return Task.FromException(new JobExecutionException(ex));
            }
        }
    }
}
