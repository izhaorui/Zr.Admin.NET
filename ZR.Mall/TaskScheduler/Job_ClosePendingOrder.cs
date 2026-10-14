using Quartz;
using ZR.Mall.Common;
using ZR.Mall.Service.IService;

namespace ZR.Mall.TaskScheduler
{
    /// <summary>
    /// 商城定时任务：关闭超时未支付的待付款订单并回补库存。
    /// 由系统任务调度触发（sys_tasks：AssemblyName=ZR.Mall，ClassName=Job_ClosePendingOrder，默认每5分钟）。
    /// SaaS 下经 Job_Dispatcher 按租户展开（TenantId="*"），在当前租户库关单；
    /// 非 SaaS 仍靠订单实体 [Tenant("MallDb")] 路由到商城库。
    /// </summary>
    /// <remarks>
    /// 超时分钟读 <see cref="MallSettingsReader.ClosePendingMinutes"/>（MallSettings:ClosePendingMinutes，默认 30）。
    /// 运行结果由 CloseExpiredPendingOrders 记录（有关单 Info / 空跑 Debug），本 Job 不再重复打日志。
    /// </remarks>
    public class Job_ClosePendingOrder : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var svc = (IOMSOrderStateService)App.GetRequiredService(typeof(IOMSOrderStateService));
                svc.CloseExpiredPendingOrders(MallSettingsReader.ClosePendingMinutes());
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
