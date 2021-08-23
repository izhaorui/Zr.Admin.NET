using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure
{
    public static class InternalApp
    {
        /// <summary>
        /// 应用服务
        /// </summary>
        internal static IServiceCollection InternalServices;

        /// <summary>
        /// 全局配置构建器
        /// </summary>
        internal static IConfigurationBuilder ConfigurationBuilder;

        /// <summary>
        /// 获取Web主机环境
        /// </summary>
        //internal static IWebHostEnvironment WebHostEnvironment;

        /// <summary>
        /// 获取泛型主机环境
        /// </summary>
        internal static IHostEnvironment HostEnvironment;
    }
}
