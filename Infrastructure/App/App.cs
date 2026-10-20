using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Serialization;
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

        /// <summary>默认响应契约：驼峰命名。两端前端都按 camelCase 取值，不可改。</summary>
        private static readonly IContractResolver CamelCaseResolver = new CamelCasePropertyNamesContractResolver();

        private static IContractResolver _responseContractResolver = CamelCaseResolver;

        /// <summary>
        /// 统一响应（ApiResult）序列化所使用的契约解析器。
        /// </summary>
        /// <remarks>
        /// <para><b>要调整响应里的字段，改这里，改宿主的 AddNewtonsoftJson 是没用的</b>：
        /// 控制器响应统一经 <c>BaseController.GetJsonStr</c> 自行序列化后以字符串返回
        ///（<c>SUCCESS</c> / <c>ToResponse</c> 都走它），根本不经过 MVC 的输出格式化器。</para>
        ///
        /// <para>公网 C 端宿主在启动时换成 FrontContractResolver，移除 SysBase 的审计字段
        ///（创建人/创建时间/更新人/更新时间/备注）；内网后台宿主保持默认 ——
        /// 后台的商品/分类/支付流水列表要展示「添加时间」这类审计字段。</para>
        /// </remarks>
        public static IContractResolver ResponseContractResolver
        {
            get => _responseContractResolver;
            // 传 null 视为恢复默认：不能静默落到 Newtonsoft 的内置默认契约上，
            // 那会让字段名从 camelCase 变成 PascalCase，一次性改坏所有前端。
            set => _responseContractResolver = value ?? CamelCaseResolver;
        }

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

            return SystemDbConfigId;
        }

        /// <summary>
        /// 系统库 ConfigId，固定为 "0"（存放租户、套餐、菜单、字典、任务等系统级数据，也是默认租户）。
        /// </summary>
        /// <remarks>
        /// 该值不再从配置读取：<c>--initdb</c> / CodeFirst 建表、<c>SqlSugarScope</c> 直查、
        /// 系统实体上的 <c>[Tenant("0")]</c> 均硬编码 0，配成其他值只会让"建表库"与"运行时读库"不一致。
        /// 配置项 <c>SystemDb</c> 已废弃，配置后被忽略并在启动时输出告警（见 <c>SqlsugarSetup.AddDb</c>）。
        /// </remarks>
        public const string SystemDbConfigId = "0";

        /// <summary>
        /// 主租户（主库）标识，等同于 <see cref="SystemDbConfigId"/>（"0"）。
        /// 租户侧大量使用 <c>GetCurrentTenantId() == MainDbConfigId</c> 判断"是否主租户"。
        /// 注意：它<b>不</b>对应配置项 "MainDb"（业务主库请用 <see cref="BizMainDbConfigId"/>）。
        /// </summary>
        public static string MainDbConfigId => SystemDbConfigId;

        /// <summary>
        /// 业务主库 ConfigId（标记 <c>[Tenant("MainDb")]</c> 的实体落在此库），未配置时回退系统库。
        /// 配置项优先 <c>BizMainDb</c>，兼容旧键 <c>MainDb</c>；单库部署无需配置。
        /// </summary>
        public static string BizMainDbConfigId => Configuration["BizMainDb"] ?? Configuration["MainDb"] ?? SystemDbConfigId;

        /// <summary>
        /// 商城库 ConfigId（仅非 SaaS 模式下商城实体使用），未配置时回退系统库。
        /// 单库部署无需配置；SaaS 下商城实体按租户库路由，本项不生效。
        /// </summary>
        public static string MallDbConfigId => Configuration["MallDb"] ?? SystemDbConfigId;

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
