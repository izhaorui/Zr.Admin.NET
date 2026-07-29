using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 业务模块初始化运行器。
    /// 统一管理各业务模块（商城、工作流等）的"建表 + 可选种子"初始化，
    /// 消除 InitTable 中每新增一个模块就复制一遍查找 / 调用 / 错误包装代码的问题。
    /// 新增独立模块只需在 Modules 字典注册一项，并在 OptionsSetting/appsettings 增加对应开关。
    /// </summary>
    public static class ModuleInitRunner
    {
        /// <summary>
        /// 模块初始化规格：建表由同名 ITenantModuleInitializer 完成；
        /// Seed 为建表后可选执行的种子逻辑（为 null 表示无需种子）。
        /// </summary>
        private sealed class ModuleSpec
        {
            public string DisplayName { get; init; }
            public Action Seed { get; init; }
        }

        /// <summary>
        /// 独立模块注册表。Key 必须与对应 ITenantModuleInitializer.ModuleName 一致。
        /// 全量 InitDb 会自动跳过此处注册的模块，改由各自开关（InitMall/InitWorkflow）驱动。
        /// </summary>
        private static readonly Dictionary<string, ModuleSpec> Modules = new()
        {
            ["Mall"] = new ModuleSpec
            {
                DisplayName = "商城",
                Seed = () =>
                {
                    var result = new SeedDataService().InitMallMenuSeedData();
                    foreach (var item in result)
                        Console.WriteLine(item);
                }
            },
            ["Workflow"] = new ModuleSpec
            {
                DisplayName = "工作流",
                Seed = () =>
                {
                    var result = new SeedDataService().InitWorkflowMenuSeedData();
                    foreach (var item in result)
                        Console.WriteLine(item);
                }
            },
        };

        /// <summary>判断某模块名是否为已注册独立模块（供全量 InitDb 跳过用）。</summary>
        public static bool Contains(string moduleName) => Modules.ContainsKey(moduleName);

        /// <summary>
        /// 运行指定独立模块的完整初始化：建表（ITenantModuleInitializer）+ 可选种子，并打印完成日志。
        /// 任一环节失败会打印并终止进程（与原 InitMall/InitWorkflow 行为一致）。
        /// </summary>
        public static void Run(string moduleName)
        {
            if (!Modules.TryGetValue(moduleName, out var spec))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ModuleInit] 未注册的模块: {moduleName}，跳过");
                Console.ResetColor();
                return;
            }

            SafeRun(() =>
            {
                RunTables(moduleName);
                spec.Seed?.Invoke();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"==== {spec.DisplayName}模块初始化完成 ====");
                Console.ResetColor();
            }, spec.DisplayName);
        }

        /// <summary>
        /// 仅执行建表（通过 ITenantModuleInitializer.InitializeNonSaaS）。用于全量 InitDb 中
        /// 逐个调度非独立模块的场景，避免调用方重复查找 initializer。
        /// </summary>
        public static void RunTables(string moduleName)
        {
            if (InternalApp.ServiceProvider == null) return;
            using var scope = InternalApp.ServiceProvider.CreateScope();
            ITenantModuleInitializer target = null;
            foreach (var mi in scope.ServiceProvider.GetServices<ITenantModuleInitializer>())
            {
                if (mi.ModuleName == moduleName)
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{displayName}模块初始化失败：{ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Environment.Exit(1);
            }
        }
    }
}
