using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security.Claims;

namespace Infrastructure
{
    public static class App
    {
        /// <summary>
        /// 服务提供器
        /// </summary>
        public static IServiceProvider ServiceProvider => HttpContext?.RequestServices ?? InternalApp.ServiceProvider;
        /// <summary>
        /// 获取请求上下文
        /// </summary>
        public static HttpContext HttpContext => HttpContextLocal.Current();
        /// <summary>
        /// 获取请求上下文用户
        /// </summary>
        public static ClaimsPrincipal User => HttpContext?.User;

        /// <summary>
        /// 获取请求生命周期的服务
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public static TService GetService<TService>()
            where TService : class
        {
            return GetService(typeof(TService)) as TService;
        }

        /// <summary>
        /// 获取请求生命周期的服务
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object GetService(Type type)
        {
            return ServiceProvider.GetService(type);
        }

        /// <summary>
        /// 获取请求生命周期的服务
        /// </summary>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public static TService GetRequiredService<TService>()
            where TService : class
        {
            return GetRequiredService(typeof(TService)) as TService;
        }

        /// <summary>
        /// 获取请求生命周期的服务
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object GetRequiredService(Type type)
        {
            return ServiceProvider.GetRequiredService(type);
        }
    }
}
