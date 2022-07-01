using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Infrastructure
{
    public static class InternalApp
    {
        /// <summary>
        /// 应用服务
        /// </summary>
        public static IServiceProvider ServiceProvider;

        /// <summary>
        /// 全局配置构建器
        /// </summary>
        //public static IConfigurationBuilder ConfigurationBuilder;

        /// <summary>
        /// 获取Web主机环境
        /// </summary>
        //internal static IWebHostEnvironment WebHostEnvironment;

        /// <summary>
        /// 获取泛型主机环境
        /// </summary>
        //public static IHostEnvironment HostEnvironment;
    }
}
