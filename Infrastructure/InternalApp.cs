using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
        public static IConfiguration Configuration;

        /// <summary>
        /// 获取Web主机环境
        /// </summary>
        public static IWebHostEnvironment WebHostEnvironment;

        /// <summary>
        /// 获取泛型主机环境
        /// </summary>
        //public static IHostEnvironment HostEnvironment;
    }
}
