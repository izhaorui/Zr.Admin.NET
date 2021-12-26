using Infrastructure.Startups;
using Microsoft.AspNetCore.Hosting;

//通过HostingStartup指定要启动的类型
[assembly: HostingStartup(typeof(HostingStartup))]

namespace Infrastructure.Startups
{
    public class HostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            // 自动装载配置
            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                // 存储环境对象
                //InternalApp.HostEnvironment = hostingContext.HostingEnvironment;
                //InternalApp.HostEnvironment = (Microsoft.Extensions.Hosting.IHostEnvironment)hostingContext.HostingEnvironment;
                // 加载配置
                //InternalApp.AddConfigureFiles(config, InternalApp.WebHostEnvironment);
                InternalApp.ConfigurationBuilder = config;
            });

            // 自动注入 AddApp() 服务
            builder.ConfigureServices(services =>
            {
                // 注册 Startup 过滤器
                //services.AddTransient<IStartupFilter, StartupFilter>();

                // 添加全局配置和存储服务提供器
                InternalApp.InternalServices = services;
            });

        }
    }
}
