using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

namespace Infrastructure
{
    public static class InternalApp
    {
        /// <summary>
        /// 应用服务
        /// </summary>
        public static IServiceProvider ServiceProvider;

        /// <summary>
        /// 当前异步流的作用域容器。
        /// 请求线程走 HttpContext.RequestServices，不需要它；
        /// Quartz 等无 HttpContext 的后台执行由 Job_Dispatcher 在租户执行期间设置，
        /// 使 App.GetRequiredService 能解析 scoped 服务（否则根容器会直接抛 Cannot resolve ... from root provider）。
        /// 用 AsyncLocal 而非静态字段：并发任务各自独立，互不覆盖。
        /// </summary>
        public static readonly AsyncLocal<IServiceProvider> ScopedServiceProvider = new();

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
