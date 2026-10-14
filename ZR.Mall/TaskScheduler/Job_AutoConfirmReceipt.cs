using Quartz;
using ZR.Mall.Common;
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
    /// <para>天数读 <see cref="MallSettingsReader.AutoConfirmReceiptDays"/>（MallSettings:AutoConfirmReceiptDays），
    /// 未配置按 7 天。运行结果由 AutoConfirmReceipt 记录（有单 Info / 空跑 Debug），本 Job 不再重复打日志。</para>
    /// </remarks>
    public class Job_AutoConfirmReceipt : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var svc = (IOMSOrderStateService)App.GetRequiredService(typeof(IOMSOrderStateService));
                svc.AutoConfirmReceipt(MallSettingsReader.AutoConfirmReceiptDays());
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
