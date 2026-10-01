using Infrastructure;
using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services.Seed
{
    /// <summary>
    /// 系统内置定时任务种子（与主库相关、与具体业务模块无关）。
    /// 工作流/商城等业务模块的内置任务已随各自 IModuleInitProvider.Seed 内聚到模块工程内，不在此处。
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
                IsStart = 0,
                TaskType = 1,
                TenantId = mainTenantId,
                Create_by = "system"
            }).ExecuteCommand();

            return "[系统任务] 写入租户到期自动停服";
        }

        /// <summary>
        /// 确保"租户到期前提醒"系统任务存在（幂等，直接查库判断）。
        /// 每日 9 点执行，对即将到期租户按 30/15/7/3/1 天阶梯发站内信（Remark 打标防重）。
        /// </summary>
        public string EnsureTenantExpireRemindTaskSeedData()
        {
            var mainTenantId = App.MainDbConfigId;
            var db = DbScoped.SugarScope.GetConnectionScope(mainTenantId);

            if (db.Queryable<SysTasks>().ClearFilter().Any(x => x.ID == "20260827000001"))
                return "[系统任务] 租户到期前提醒已存在，跳过";

            db.Insertable(new SysTasks
            {
                ID = "20260827000001",
                Name = "租户到期前提醒",
                JobGroup = "system",
                Cron = "0 0 9 * * ?",
                AssemblyName = "ZR.ServiceCore",
                ClassName = "Job_TenantExpireRemind",
                TriggerType = 1,
                IntervalSecond = 0,
                IsStart = 0,
                TaskType = 1,
                TenantId = mainTenantId,
                Create_by = "system"
            }).ExecuteCommand();

            return "[系统任务] 写入租户到期前提醒";
        }

    }
}
