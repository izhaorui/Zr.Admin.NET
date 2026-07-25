using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Security.Claims;

namespace Infrastructure
{
    public static class App
    {
        /// <summary>
        /// 全局配置文件
        /// </summary>
        public static OptionsSetting OptionsSetting => CatchOrDefault(() => ServiceProvider?.GetService<IOptions<OptionsSetting>>()?.Value);

        /// <summary>
        /// 服务提供器
        /// </summary>
        public static IServiceProvider ServiceProvider => InternalApp.ServiceProvider;
        /// <summary>
        /// 获取请求上下文
        /// </summary>
        public static HttpContext HttpContext => CatchOrDefault(() => ServiceProvider?.GetService<IHttpContextAccessor>()?.HttpContext);
        /// <summary>
        /// 获取请求上下文用户
        /// </summary>
        public static ClaimsPrincipal User => HttpContext?.User;
        /// <summary>
        /// 获取用户名
        /// </summary>
        public static string UserName => User?.Identity?.Name;
        /// <summary>
        /// 获取Web主机环境
        /// </summary>
        public static IWebHostEnvironment WebHostEnvironment => InternalApp.WebHostEnvironment;
        /// <summary>
        /// 获取全局配置
        /// </summary>
        public static IConfiguration Configuration => CatchOrDefault(() => InternalApp.Configuration, new ConfigurationBuilder().Build());
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
        /// 当前可用的服务提供器：请求期间使用 HttpContext.RequestServices，非请求场景回退到根容器
        /// </summary>
        private static IServiceProvider CurrentServiceProvider => HttpContext?.RequestServices ?? ServiceProvider;

        /// <summary>
        /// 获取请求生命周期的服务
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object GetService(Type type)
        {
            return CurrentServiceProvider.GetService(type);
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
            return CurrentServiceProvider.GetRequiredService(type);
        }

        /// <summary>
        /// 处理获取对象异常问题
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="action">获取对象委托</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>T</returns>
        private static T CatchOrDefault<T>(Func<T> action, T defaultValue = null)
            where T : class
        {
            try
            {
                return action();
            }
            catch
            {
                return defaultValue ?? null;
            }
        }

        /// <summary>
        /// 获取默认租户ID
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentTenantId()
        {
            if (!string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId))
            {
                return TenantContext.CurrentTenantId;
            }

            var itemId = HttpContext?.Items?["TenantId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                return itemId;
            }

            var headerId = HttpContext?.Request?.Headers["tenantId"].ToString();
            if (!string.IsNullOrWhiteSpace(headerId))
            {
                return headerId;
            }

            var claimId = User?.Claims.FirstOrDefault(f => f.Type == ClaimTypes.PrimaryGroupSid)?.Value;
            if (!string.IsNullOrWhiteSpace(claimId))
            {
                return claimId;
            }

            return MainDbConfigId;
        }

        /// <summary>
        /// 主库 ConfigId（多租户架构中存放租户、套餐等共享数据），默认 "0"。
        /// </summary>
        public static string MainDbConfigId => Configuration["MainDb"] ?? "0";

        /// <summary>
        /// 商城库 ConfigId（非SaaS模式下商城实体使用此 ConfigId），默认 "1"。
        /// 在 appsettings.json 中通过 "MallDb" 配置项指定。
        /// </summary>
        public static string MallDbConfigId => Configuration["MallDb"] ?? "0";

        /// <summary>
        /// 是否启用多租户。
        /// </summary>
        /// <returns></returns>
        public static bool IsTenantEnabled()
        {
            var useTenant = Configuration["TenantSettings:UseTenant"];
            //Console.WriteLine($"是否启用多租户: {useTenant}");
            if (string.IsNullOrWhiteSpace(useTenant))
            {
                return false;
            }

            if (bool.TryParse(useTenant, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(useTenant, out var intValue))
            {
                return intValue == 1;
            }

            return string.Equals(useTenant, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(useTenant, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(useTenant, "enabled", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 租户根域名列表（如 shop.com）。子域名解析时据此剥离出子域标签（a.shop.com → a）。
        /// 在 appsettings.json 的 TenantSettings:RootDomains 配置。
        /// </summary>
        public static string[] GetTenantRootDomains()
        {
            var arr = Configuration.GetSection("TenantSettings:RootDomains").Get<string[]>();
            return arr ?? [];
        }

        /// <summary>
        /// 保留子域名（如 www/admin/api/doc）。这些子域名不解析为租户，维持主库/原机制。
        /// 未配置时返回默认值 www/admin/api/doc。
        /// </summary>
        public static string[] GetTenantReservedSubDomains()
        {
            var arr = Configuration.GetSection("TenantSettings:ReservedSubDomains").Get<string[]>();
            if (arr != null && arr.Length > 0)
            {
                return arr;
            }

            return new[] { "www", "admin", "api", "doc" };
        }

    }
}
