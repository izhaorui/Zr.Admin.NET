using Quartz;
using ZR.Mall.Service.IService;

namespace ZR.Mall.TaskScheduler
{
    /// <summary>
    /// 商城定时任务：发货后超过 N 天未操作的订单自动确认收货。
    /// </summary>
    /// <remarks>
    /// <para>由系统任务调度触发（sys_tasks：AssemblyName=ZR.Mall，ClassName=Job_AutoConfirmReceipt，默认每小时）。
    /// 没有它订单会永远停在「已发货」，售后期限与商家结算都无法收口。</para>
    ///
    /// <para>SaaS 下经 Job_Dispatcher 按租户展开（TenantId="*"），在当前租户库处理；
    /// 非 SaaS 仍靠订单实体 [Tenant("MallDb")] 路由到商城库。</para>
    ///
    /// <para>天数读 appsettings 的 <c>MallSettings:AutoConfirmReceiptDays</c>，未配置按 7 天。</para>
    /// </remarks>
    public class Job_AutoConfirmReceipt : IJob
    {
        /// <summary>未配置时的默认天数</summary>
        private const int DefaultDays = 7;

        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var svc = (IOMSOrderStateService)App.GetRequiredService(typeof(IOMSOrderStateService));
                var days = ResolveDays();
                var confirmed = svc.AutoConfirmReceipt(days);
                Console.WriteLine($"[Job_AutoConfirmReceipt] 已自动确认收货 {confirmed} 笔（发货后 {days} 天）");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // 交给调度器记录失败日志
                return Task.FromException(new JobExecutionException(ex));
            }
        }

        /// <summary>
        /// 读取自动确认天数。
        /// </summary>
        /// <remarks>
        /// 配置缺失或非法一律退回默认值，不抛异常：本任务每小时一轮，
        /// 为一个可选的配置项让整轮失败（后面还会被调度器记为任务失败）不值得。
        /// </remarks>
        private static int ResolveDays()
        {
            var raw = App.Configuration?["MallSettings:AutoConfirmReceiptDays"];
            return int.TryParse(raw, out var days) && days > 0 ? days : DefaultDays;
        }
    }
}
