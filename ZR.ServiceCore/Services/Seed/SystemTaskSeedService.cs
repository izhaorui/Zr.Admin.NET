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
    }
}
