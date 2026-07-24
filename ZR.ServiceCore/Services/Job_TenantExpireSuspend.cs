using Infrastructure;
using Infrastructure.Attribute;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 租户到期自动停服任务。
    /// 由统一分发器 Job_Dispatcher 通过反射调用 Run()（TenantId 设为主库，仅执行一次）。
    /// </summary>
    [AppService(ServiceType = typeof(Job_TenantExpireSuspend), ServiceLifetime = LifeTime.Scoped)]
    public class Job_TenantExpireSuspend
    {
        private readonly ISysTenantService _tenantService;

        public Job_TenantExpireSuspend(ISysTenantService tenantService)
        {
            _tenantService = tenantService;
        }

        public void Run()
        {
            var count = _tenantService.SuspendExpiredTenants("system");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[定时任务] 租户到期自动停服：本次停服 {count} 个租户");
            Console.ResetColor();
        }
    }
}
