using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        /// </summary>
        public static void InitDb(bool init, IWebHostEnvironment env)
        {
            if (!init) return;

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
        /// 开发环境初始化入口
        /// </summary>
        public static void RunInitDb(IWebHostEnvironment environment)
        {
            var options = App.OptionsSetting;

            // 全量数据库初始化（仅 Development + InitDb=true，带交互确认）
            if (environment.IsDevelopment() && options.InitDb)
            {
                bool confirmed;
                if (!Console.IsInputRedirected)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("检测到 InitDb=true，将执行数据库初始化（自动迁移 + 种子数据）。");
                    Console.WriteLine("按 [回车] 确认执行，按其他键取消：");

                    var input = ReadLineWithTimeout(TimeSpan.FromSeconds(30))?.Trim();
                    confirmed = string.IsNullOrEmpty(input);
                }
                else
                {
                    Console.WriteLine("非交互环境，直接执行数据库初始化。");
                    confirmed = true;
                }

                if (!confirmed)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("已取消初始化。如需正常启动请将 InitDb 改为 false。");
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                try
                {
                    InitDb(options.InitDb, environment);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"数据库迁移失败：{ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                try
                {
                    InitSeedData(environment);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"初始化种子数据失败：{ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    Console.ResetColor();
                    Environment.Exit(1);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==== 数据库初始化完成 ====");
                Console.ResetColor();
            }

            // 按 appsettings 开关批量运行独立业务模块（商城/工作流等），
            // 受各自开关控制、独立于 InitDb，可在非开发环境单独开启自动补列。
            ModuleInitRunner.RunEnabledModules(options);
        }

        private static string ReadLineWithTimeout(TimeSpan timeout)
        {
            var task = Task.Run(() => Console.ReadLine());
            return task.Wait(timeout) ? task.Result : null;
        }

        public static void InitSeedData(IWebHostEnvironment environment)
        {
            var path = Path.Combine(environment.WebRootPath, "data.xlsx");
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[种子数据] 未找到 {path}，跳过自动初始化");
                Console.ResetColor();
                return;
            }

            try
            {
                SeedDataService seedDataService = new();
                var result = seedDataService.InitSeedData(path, false);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==== 种子数据初始化完成 ====");
                foreach (var item in result)
                    Console.WriteLine(item);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[种子数据] 自动初始化失败，事务已回滚: {ex.Message}");
                Console.ResetColor();
            }
        }

    }
}
