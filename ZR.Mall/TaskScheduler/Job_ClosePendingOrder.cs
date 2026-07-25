using Quartz;
using ZR.Mall.Service.IService;

namespace ZR.Mall.TaskScheduler
{
    /// <summary>
    /// 商城定时任务：关闭超时未支付的待付款订单并回补库存。
    /// 由系统任务调度触发（sys_tasks：AssemblyName=ZR.Mall，ClassName=Job_ClosePendingOrder，默认每5分钟）。
    /// 商城数据固定走 MallDb，与租户无关，任务 TenantId 设为主库即可单次执行（OMSOrderService 内部已固定连接）。
    /// </summary>
    public class Job_ClosePendingOrder : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var svc = (IOMSOrderService)App.GetRequiredService(typeof(IOMSOrderService));
                var closed = svc.CloseExpiredPendingOrders(30); // 30 分钟未支付自动取消
                Console.WriteLine($"[Job_ClosePendingOrder] 已自动取消 {closed} 笔超时待付款订单");
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
