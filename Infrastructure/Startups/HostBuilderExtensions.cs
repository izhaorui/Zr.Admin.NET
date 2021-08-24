using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Startups
{
    /// <summary>
    /// Program.cs里面的HostBuilder扩展
    /// </summary>
    public static class HostBuilderExtensions
    {
        /// <summary>
        /// Web 主机注入
        /// </summary>
        /// <param name="hostBuilder">Web主机构建器</param>
        /// <param name="assemblyName">外部程序集名称，如果HostingStartup存在多个程序集中可以使用;分隔,比如HostStartupLib;HostStartupLib2</param>
        /// <returns>IWebHostBuilder</returns>
        public static IWebHostBuilder Init(this IWebHostBuilder hostBuilder, string assemblyName)
        {
            hostBuilder.UseSetting(WebHostDefaults.HostingStartupAssembliesKey, assemblyName);
            return hostBuilder;
        }

        /// <summary>
        /// 初始化程序扩展
        /// </summary>
        /// <param name="builder"></param>
        public static void UseAppStartup(this IWebHostBuilder hostBuilder)
        {
            // 自动装载配置
            hostBuilder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                // 存储环境对象
                InternalApp.HostEnvironment = (IHostEnvironment)hostingContext.HostingEnvironment;

                // 加载配置
                //InternalApp.AddConfigureFiles(config, InternalApp.WebHostEnvironment);
            });
            // 自动注入 AddApp() 服务
            hostBuilder.ConfigureServices((services) =>
            {
                // 注册 Startup 过滤器
                //services.AddTransient<IStartupFilter, StartupFilter>();

                // 添加全局配置和存储服务提供器
                InternalApp.InternalServices = services;

                // 初始化应用服务
                //services.AddApp();
            });
        }
    }
}
