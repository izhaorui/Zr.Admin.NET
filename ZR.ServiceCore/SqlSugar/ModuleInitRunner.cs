using Infrastructure;
using Infrastructure.Model;
using Microsoft.Extensions.DependencyInjection;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 业务模块初始化运行器。
    /// 统一管理各业务模块（商城、工作流等）的"建表 + 可选种子"初始化，
    /// 消除 InitTable 中每新增一个模块就复制一遍查找 / 调用 / 错误包装代码的问题。
    /// 新增独立模块只需在 Modules 字典注册一项（含 DisplayName、可选 Seed、可选开关），
    /// 全量 InitDb 与按开关的独立初始化均从此处驱动，调用方不再逐模块写重复分支。
    /// </summary>
    public static class ModuleInitRunner
    {
        /// <summary>
        /// 模块初始化规格：建表由同名 ITenantModuleInitializer 完成；
        /// Seed 为建表后可选执行的"非菜单"种子逻辑（为 null 表示无需）；
        /// IsEnabled 返回该模块对应的 appsettings 开关是否开启（为 null 表示无独立开关，随全量初始化）。
        /// 菜单种子（InitXxxMenuSeedData）不在此处，统一由 MenuSeeds + SeedMenu 按开关写入，确保"启用模块才写菜单"。
        /// </summary>
        private sealed class ModuleSpec
        {
            public string DisplayName { get; init; }
            public Action Seed { get; init; }
            public Func<OptionsSetting, bool> IsEnabled { get; init; }
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
                IsEnabled = o => o.InitMall
            },
            ["Workflow"] = new ModuleSpec
            {
                DisplayName = "工作流",
                IsEnabled = o => o.InitWorkflow
            },
            ["Saas"] = new ModuleSpec
            {
                DisplayName = "SaaS",
                IsEnabled = o => o.InitSaasMenu
            }
        };

        /// <summary>
        /// 各模块的"菜单种子"工厂：仅在对应模块开关开启时才调用，写入菜单与按钮权限。
        /// 与建表解耦，避免模块未启用时误写菜单数据。
        /// </summary>
        private static readonly Dictionary<string, Func<List<string>>> MenuSeeds = new()
        {
            ["Mall"] = () => new SeedDataService().InitMallMenuSeedData(),
            ["Workflow"] = () => new SeedDataService().InitWorkflowMenuSeedData(),
            ["Saas"] = () => new SeedDataService().InitSaasMenuSeedData()
        };

        /// <summary>判断某模块名是否为已注册独立模块（供全量 InitDb 跳过用）。</summary>
        public static bool Contains(string moduleName) => Modules.ContainsKey(moduleName);

        /// <summary>
        /// 按 OptionsSetting 中各模块开关，批量运行所有开启的独立模块初始化（建表 + 菜单种子）。
        /// 取代 InitTable.RunInitDb 中逐模块写 if (options.InitXxx) Run("Xxx") 的重复分支；
        /// 新增带开关的模块只需在 Modules 字典注册并填 IsEnabled，此处自动覆盖。
        /// 菜单种子严格在 IsEnabled 确认后写入，未启用模块绝不写菜单数据。
        /// </summary>
        public static void RunEnabledModules(OptionsSetting options)
        {
            foreach (var (name, spec) in Modules)
            {
                Console.WriteLine($"==== 检查 {spec.DisplayName} 模块是否启用{spec.IsEnabled?.Invoke(options)} ====");
                if (spec.IsEnabled?.Invoke(options) == true)
                {
                    Run(name);
                    SeedMenu(name);
                }
            }
        }

        /// <summary>
        /// 仅写入指定模块的菜单种子（InitXxxMenuSeedData）。供 RunEnabledModules 在确认模块启用后调用；
        /// 未注册的模块静默跳过。菜单种子与建表解耦，确保"启用模块才写菜单"。
        /// </summary>
        public static void SeedMenu(string moduleName)
        {
            if (MenuSeeds.TryGetValue(moduleName, out var seed))
            {
                foreach (var line in seed())
                    Console.WriteLine(line);
            }
        }

        /// <summary>
        /// 运行指定独立模块的建表初始化（ITenantModuleInitializer.InitializeNonSaaS）+ 可选非菜单种子，
        /// 并打印完成日志。任一环节失败会打印并终止进程（与原 InitMall/InitWorkflow 行为一致）。
        /// 菜单种子不在此处，统一由 SeedMenu 按开关写入。
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
