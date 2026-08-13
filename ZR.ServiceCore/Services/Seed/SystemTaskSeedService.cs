using Infrastructure;
using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 系统内置定时任务种子（与主库相关、与具体业务模块无关）。
    /// </summary>
    internal sealed class SystemTaskSeedService
    {
        /// <summary>
        /// 确保"租户到期自动停服"系统任务存在（幂等，直接查库判断）。
        /// </summary>
        public string EnsureSystemTasksSeedData()
        {
            var mainTenantId = App.MainDbConfigId;
            var db = DbScoped.SugarScope.GetConnectionScope(mainTenantId);

            if (db.Queryable<SysTasks>().ClearFilter().Any(x => x.ID == "20260725000001"))
                return "[系统任务] 租户到期自动停服已存在，跳过";

            db.Insertable(new SysTasks
            {
                ID = "20260725000001",
                Name = "租户到期自动停服",
                JobGroup = "system",
                Cron = "0 0 2 * * ?",
                AssemblyName = "ZR.ServiceCore",
                ClassName = "Job_TenantExpireSuspend",
                TriggerType = 1,
                IntervalSecond = 0,
                IsStart = 1,
                TaskType = 1,
                TenantId = mainTenantId,
                Create_by = "system"
            }).ExecuteCommand();

            return "[系统任务] 写入租户到期自动停服";
        }

        /// <summary>
        /// 确保"工作流超时自动处理"系统任务存在（幂等，直接查库判断）。
        /// 工作流数据在租户库，TenantId 设 "*" 由 Job_Dispatcher 按所有启用租户展开执行，
        /// 每个租户上下文内扫描超时待办并自动通过/驳回/转交。默认每 5 分钟（Cron 0 0/5 * * * ?）。
        /// </summary>
        public string EnsureWorkflowTimeoutTaskSeedData()
        {
            var mainTenantId = App.MainDbConfigId;
            var db = DbScoped.SugarScope.GetConnectionScope(mainTenantId);

            if (db.Queryable<SysTasks>().ClearFilter().Any(x => x.ID == "20260813000001"))
                return "[系统任务] 工作流超时自动处理已存在，跳过";

            db.Insertable(new SysTasks
            {
                ID = "20260813000001",
                Name = "工作流超时自动处理",
                JobGroup = "workflow",
                Cron = "0 0/5 * * * ?",
                AssemblyName = "ZR.Workflow",
                ClassName = "Job_WfTimeoutAutoProcess",
                TriggerType = 1,
                IntervalSecond = 0,
                IsStart = 1,
                TaskType = 1,
                TenantId = "*",
                Create_by = "system"
            }).ExecuteCommand();

            return "[系统任务] 写入工作流超时自动处理";
        }
    }
}
