using Infrastructure.Attribute;
using Infrastructure.Helper;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Infrastructure
{
    /// <summary>
    /// App服务注册
    /// </summary>
    public static class AppServiceExtensions
    {
        /// <summary>自动扫描的业务程序集名前缀：引用闭包中以此前缀命名的程序集参与 [AppService] 扫描。</summary>
        private const string ScanAssemblyPrefix = "ZR.";

        /// <summary>
        /// 自动扫描 ZR.* 业务程序集（含 appsettings InjectClass 补充数组）中所有 AppService 标注的类并注册。
        /// 新增 ZR.* 模块工程零配置自动生效；InjectClass 仅用于登记非常规命名的插件程序集。
        /// </summary>
        public static void AddAppService(this IServiceCollection services)
        {
            var assemblies = ResolveScanAssemblies(Assembly.GetEntryAssembly() ?? typeof(AppServiceExtensions).Assembly);

            var registered = 0;
            foreach (var assembly in assemblies)
            {
                var count = Register(services, assembly);
                registered += count;
                // 逐程序集输出注册数：让"扫进了什么、注册了什么"完全可见；0 注册的程序集不刷屏
                if (count > 0)
                {
                    Log.WriteLine(ConsoleColor.DarkGray, $"[AppService] {assembly.GetName().Name} 注册 {count} 个服务");
                }
            }

            Log.WriteLine(ConsoleColor.DarkCyan, $"[AppService] 扫描 {assemblies.Count} 个程序集，共注册 {registered} 个服务");
        }

        /// <summary>
        /// 解析需要扫描 [AppService] 的程序集：
        /// 1) 以 root 为入口做引用闭包 BFS，收集 ZR.* 前缀的业务程序集（第三方程序集不纳入、不下钻，
        ///    以控制反射加载面并规避第三方 dll 的类型加载异常；ZR 工程均被宿主直接引用，深度 1 即全覆盖）；
        /// 2) 合并 appsettings InjectClass 补充数组（非常规命名的插件程序集），按程序集名去重。
        /// 加载失败逐项容错：跳过并输出日志，不中断启动。
        /// </summary>
        public static List<Assembly> ResolveScanAssemblies(Assembly root)
        {
            var result = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(Assembly assembly)
            {
                var name = assembly.GetName().Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result.TryAdd(name, assembly);
                }
            }

            // 入口/指定根程序集始终纳入扫描（宿主程序集也可能标注 [AppService]）
            TryAdd(root);

            // 输出目录物理扫描（与控制器/校验器装配同源，见 AssemblyUtils）。
            // ⚠️ 不可只依赖引用闭包：编译器会把"宿主代码未直接使用"的程序集引用从产物中裁掉
            //（Assembly.GetReferencedAssemblies() 不含它们），但此类"仅传递引用"的业务模块
            // 的控制器仍会被宿主从输出目录装配 —— 闭包扫描漏掉它就会出现
            // "控制器可达、[AppService] 服务未注册"的运行期 500（DI 解析失败）。
            foreach (var assembly in AssemblyUtils.GetAssemblies())
            {
                if (IsScanCandidate(assembly))
                {
                    TryAdd(assembly);
                }
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<AssemblyName>(root.GetReferencedAssemblies());
            while (queue.Count > 0)
            {
                var reference = queue.Dequeue();
                if (reference?.Name == null || !visited.Add(reference.Name))
                {
                    continue;
                }

                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(reference);
                }
                catch (Exception ex)
                {
                    // 闭包中的程序集可能加载失败（可选依赖等），跳过不影响其余扫描
                    Log.WriteLine(ConsoleColor.DarkGray, $"[AppService] 程序集 {reference.Name} 加载失败，已跳过: {ex.Message}");
                    continue;
                }

                // 非 ZR.* 的第三方程序集：不纳入扫描，也不继续下钻
                if (!IsScanCandidate(assembly))
                {
                    continue;
                }

                TryAdd(assembly);
                foreach (var dependency in assembly.GetReferencedAssemblies())
                {
                    queue.Enqueue(dependency);
                }
            }

            // InjectClass 补充数组：登记非常规命名的插件程序集（缺省空数组；加载失败只告警跳过）
            foreach (var name in GetInjectClassExtras())
            {
                if (string.IsNullOrWhiteSpace(name) || result.ContainsKey(name.Trim()))
                {
                    continue;
                }

                try
                {
                    TryAdd(Assembly.Load(name.Trim()));
                }
                catch (Exception ex)
                {
                    Log.WriteLine(ConsoleColor.Yellow, $"[AppService] 补充程序集 {name.Trim()} 加载失败，已跳过: {ex.Message}");
                }
            }

            return result.Values.ToList();
        }

        /// <summary>
        /// 注册单个程序集内全部 AppService 标注类，返回注册数量。
        /// GetTypes 遇不可加载类型时降级取 ReflectionTypeLoadException 中的非空类型，不让单个程序集中断启动。
        /// </summary>
        public static int Register(IServiceCollection services, Assembly assembly)
        {
            var registered = 0;
            foreach (var type in GetTypesSafe(assembly))
            {
                var serviceAttribute = type.GetCustomAttribute<AppServiceAttribute>();

                if (serviceAttribute == null)
                {
                    continue;
                }

                var serviceType = serviceAttribute.ServiceType;
                //情况1 未指定 ServiceType 时按约定推断：优先取与类名对应的接口（SysConfigService → ISysConfigService），
                //      使 [AppService] 可以裸写；类名与接口名不对应（如 DefaultSmsSender : ISmsSender）仍需显式指定。
                //      约定匹配优于 GetInterfaces().FirstOrDefault() —— 后者的接口顺序在 CLR 中无保证。
                if (serviceType == null)
                {
                    serviceType = type.GetInterfaces().FirstOrDefault(i =>
                        string.Equals(i.Name, $"I{type.Name}", StringComparison.OrdinalIgnoreCase));
                }
                //情况2 显式声明 InterfaceServiceType 且约定未命中时，回退取第一个接口（保持既有语义）
                if (serviceType == null && serviceAttribute.InterfaceServiceType)
                {
                    serviceType = type.GetInterfaces().FirstOrDefault();
                }
                //情况3 无接口（或约定未命中且未声明 InterfaceServiceType）：注册为自身
                if (serviceType == null)
                {
                    serviceType = type;
                }

                switch (serviceAttribute.ServiceLifetime)
                {
                    case LifeTime.Singleton:
                        services.AddSingleton(serviceType, type);
                        break;
                    case LifeTime.Scoped:
                        services.AddScoped(serviceType, type);
                        break;
                    case LifeTime.Transient:
                        services.AddTransient(serviceType, type);
                        break;
                    default:
                        services.AddTransient(serviceType, type);
                        break;
                }
                registered++;
            }
            return registered;
        }

        /// <summary>是否为需要扫描 [AppService] 的业务程序集：ZR.* 前缀约定。</summary>
        private static bool IsScanCandidate(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return !string.IsNullOrWhiteSpace(name)
                && name.StartsWith(ScanAssemblyPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>读取 InjectClass 补充数组。AppSettings 静态配置未初始化（如单元测试宿主）时安全返回空数组。</summary>
        private static string[] GetInjectClassExtras()
        {
            try
            {
                return AppSettings.Get<string[]>("InjectClass") ?? Array.Empty<string>();
            }
            catch
            {
                // 静态 Configuration 未初始化等场景：无补充数组属合法状态，不视为错误
                return Array.Empty<string>();
            }
        }

        /// <summary>容错取程序集全部类型：ReflectionTypeLoadException 时仅取已成功加载的非空类型。</summary>
        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
