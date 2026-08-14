using Infrastructure;
using Quartz;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.TaskScheduler
{
    /// <summary>
    /// 工作流 Webhook Outbox 重试定时任务。
    /// 由系统任务调度（sys_tasks：AssemblyName=ZR.Workflow，ClassName=Job_WfWebhookRetry，默认每 2 分钟），
    /// 经 Job_Dispatcher 按租户展开（TenantId="*"），在每个租户上下文内扫描并投递待发 Webhook。
    /// 工作流数据在租户库，因此必须走 Dispatcher 的租户展开机制。
    /// </summary>
    public class Job_WfWebhookRetry : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var engine = (IWfEngineService)App.GetRequiredService(typeof(IWfEngineService));
                engine.RetryWebhookDeliveries();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(new JobExecutionException(ex));
            }
        }
    }
}
