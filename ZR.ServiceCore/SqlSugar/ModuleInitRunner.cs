using Infrastructure;
using Infrastructure.Model;
using Infrastructure.ModuleInit;
using Microsoft.Extensions.DependencyInjection;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 业务模块初始化运行器。
    /// <para>
    /// 通过依赖注入发现所有 <see cref="IModuleInitProvider"/> 实现，按 <see cref="IModuleInitProvider.Order"/> 升序
    /// 依次执行「建表（同名 ITenantModuleInitializer）→ 非菜单种子 → 菜单种子」。
    /// </para>
    /// <para>
    /// 框架层不再硬编码任何具体模块名；新增业务模块只需在自己工程内实现
    /// <see cref="IModuleInitProvider"/> 并标注 <c>[AppService(ServiceType = typeof(IModuleInitProvider))]</c>，
    /// 即可自动纳入 <c>--initdb</c> 流程（建表器 + 种子全部内聚在模块工程内，框架零改动）。
    /// </para>
    /// </summary>
    public static class ModuleInitRunner
    {
        /// <summary>
        /// 判断某模块名是否为已注册的独立模块。供全量初始化（InitTable.InitDb）跳过用，
        /// 避免与 <see cref="RunEnabledModules"/> 重复执行同一模块的建表。
        /// </summary>
        public static bool Contains(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName)) return false;
            var sp = InternalApp.ServiceProvider;
            if (sp == null) return false;

            using var scope = sp.CreateScope();
            return scope.ServiceProvider.GetServices<IModuleInitProvider>()
                .Any(p => string.Equals(p.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 按 OptionsSetting 中各模块开关，批量运行所有开启的独立模块初始化（建表 + 非菜单种子 + 菜单种子）。
        /// 由 CLI <c>--initdb</c>（InitTable.RunInitDb）触发。
        /// <para>
        /// 执行顺序由各 provider 的 <see cref="IModuleInitProvider.Order"/> 显式声明（升序）。
        /// 顺序约束：写菜单的模块必须排在消费菜单的模块之前 —— Pro 的 AI 菜单必须先于
        /// Saas 的默认套餐同步（EnsureTenantPlanMenuSeedData 会把当时的非平台菜单写进套餐）。
        /// </para>
        /// </summary>
        public static void RunEnabledModules(OptionsSetting options)
        {
            var sp = InternalApp.ServiceProvider;
            if (sp == null)
            {
                Log.WriteLine(ConsoleColor.Yellow, "[ModuleInit] 服务容器未初始化，跳过模块初始化");
                return;
            }

            // 全部 provider 在同一 scope 内解析与执行：providers 多为无状态实现，
            // 但统一生命周期可避免"解析后容器释放实例"的隐患。
            using var scope = sp.CreateScope();
            var providers = scope.ServiceProvider.GetServices<IModuleInitProvider>()
                .OrderBy(p => p.Order)
                .ToList();

            if (providers.Count == 0)
            {
                Log.WriteLine(ConsoleColor.Yellow, "[ModuleInit] 未发现任何 IModuleInitProvider 实现，跳过");
                return;
            }

            foreach (var provider in providers)
            {
                bool enabled;
                try
                {
                    enabled = provider.IsEnabled(options);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(ConsoleColor.Red, $"{provider.DisplayName}模块开关判定失败：{ex.Message}");
                    Log.WriteLine(ConsoleColor.Red, ex.StackTrace);
                    continue;
                }

                Log.WriteLine(ConsoleColor.Cyan, $"==== 检查 {provider.DisplayName} 模块是否启用{enabled} ====");
                if (!enabled) continue;

                SafeRun(() =>
                {
                    RunTables(provider.ModuleName);
                    provider.Seed();

                    foreach (var line in provider.SeedMenu())
                    {
                        Log.WriteLine(ConsoleColor.White, line);
                    }

                    Log.WriteLine(ConsoleColor.Green, $"==== {provider.DisplayName}模块初始化完成 ====");
                }, provider.DisplayName);
            }
        }

        /// <summary>
        /// 仅执行指定模块的建表（通过同名 ITenantModuleInitializer.InitializeNonSaaS）。
        /// 用于按模块名调度建表，避免调用方重复查找 initializer。
        /// </summary>
        public static void RunTables(string moduleName)
        {
            var sp = InternalApp.ServiceProvider;
            if (sp == null) return;

            using var scope = sp.CreateScope();
            ITenantModuleInitializer target = null;
            foreach (var mi in scope.ServiceProvider.GetServices<ITenantModuleInitializer>())
            {
                if (string.Equals(mi.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    target = mi;
                    break;
                }
            }
            target?.InitializeNonSaaS();
        }

        private static void SafeRun(Action action, string displayName)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"{displayName}模块初始化失败：{ex.Message}");
                Log.WriteLine(ConsoleColor.Red, ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
