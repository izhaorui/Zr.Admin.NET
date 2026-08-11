using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar.IOC;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 初始化表（已重构：自动发现实体 → 差异报告 → 迁移历史 → 单实体容错）
    /// </summary>
    public class InitTable
    {
        /// <summary>
        /// 自动创建db、表、补充缺失列。实体由 DbMigrationService 显式注册表管理。
        /// 由 CLI 参数 --initdb 显式触发（见 RunInitDb），不需再用开关二次把关。
        /// </summary>
        public static void InitDb(IWebHostEnvironment env)
        {
            var db = DbScoped.SugarScope;

            // 核心：显式注册表实体 → 差异检测 → CodeFirst 迁移（逐个容错）→ 报告输出 + 历史记录
            var report = DbMigrationService.Migrate(db, env);

            if (report.Success)
            {
                // 确保默认租户存在（收敛到种子数据 SeedDataService.EnsureDefaultTenant）
                new SeedDataService().EnsureDefaultTenant();

                // 调度各业务模块的非SaaS初始化（如商城、内容等）
                if (InternalApp.ServiceProvider != null)
                {
                    using var scope = InternalApp.ServiceProvider.CreateScope();
                    var moduleInitializers = scope.ServiceProvider.GetServices<ITenantModuleInitializer>();
                    foreach (var mi in moduleInitializers)
                    {
                        // 已注册为独立模块的（商城/工作流）由各自开关控制，避免与全量初始化重复执行
                        if (ModuleInitRunner.Contains(mi.ModuleName)) continue;
                        mi.InitializeNonSaaS();
                    }
                }
            }
        }

        /// <summary>
        /// 显式触发的数据库初始化入口（由 CLI 参数 --initdb 驱动）。
        /// 不再耦合进 Web 启动主链路，避免每次启动被阻塞或拖慢。
        /// 全量迁移 + 种子数据，异常时打印并终止进程（部署动作，失败即停）。
        /// </summary>
        public static void RunInitDb(IWebHostEnvironment environment)
        {
            var options = App.OptionsSetting;

            Log.WriteLine(ConsoleColor.Cyan, "==== 开始数据库初始化（自动迁移 + 种子数据）====");
            try
            {
                InitDb(environment);
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"数据库迁移失败：{ex.Message}");
                Log.WriteLine(ConsoleColor.Red, ex.StackTrace);
                Environment.Exit(1);
            }

            try
            {
                InitSeedData(environment);
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"初始化种子数据失败：{ex.Message}");
                Log.WriteLine(ConsoleColor.Red, ex.StackTrace);
                Environment.Exit(1);
            }

            // 按 appsettings 开关批量运行独立业务模块（商城/工作流等），
            // 受各自开关控制、独立于全量初始化，可在部署时单独开启自动补列。
            ModuleInitRunner.RunEnabledModules(options);

            Log.WriteLine(ConsoleColor.Green, "==== 数据库初始化完成 ====");
        }

        public static void InitSeedData(IWebHostEnvironment environment)
        {
            var path = Path.Combine(environment.WebRootPath, "data.xlsx");
            if (!File.Exists(path))
            {
                Log.WriteLine(ConsoleColor.Yellow, $"[种子数据] 未找到 {path}，跳过自动初始化");
                return;
            }

            try
            {
                SeedDataService seedDataService = new();
                var result = seedDataService.InitSeedData(path, false);

                Log.WriteLine(ConsoleColor.Green, "==== 种子数据初始化完成 ====");
                foreach (var item in result)
                    Log.WriteLine(ConsoleColor.Green, item);
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[种子数据] 自动初始化失败，事务已回滚: {ex.Message}");
            }
        }

    }
}
