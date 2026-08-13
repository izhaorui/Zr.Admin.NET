using Infrastructure;
using Quartz;
using ZR.Workflow.Service;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.TaskScheduler
{
    /// <summary>
    /// 工作流超时自动处理定时任务。
    /// 由系统任务调度（sys_tasks：AssemblyName=ZR.Workflow，ClassName=Job_WfTimeoutAutoProcess，默认每 5 分钟），
    /// 经 Job_Dispatcher 按租户展开（TenantId="*"），在每个租户上下文内扫描超时待办并自动处理。
    /// 工作流数据在租户库，因此必须走 Dispatcher 的租户展开机制，而非单库 Job。
    /// </summary>
    public class Job_WfTimeoutAutoProcess : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var engine = (IWfEngineService)App.GetRequiredService(typeof(IWfEngineService));
                engine.ProcessTimeoutTasks();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // 交给调度器记录失败日志（Dispatcher 已按租户包裹 try/catch 记 SysTasksLog）
                return Task.FromException(new JobExecutionException(ex));
            }
        }
    }
}
