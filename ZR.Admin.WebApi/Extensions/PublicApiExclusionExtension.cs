using Infrastructure.Attribute;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ZR.Admin.WebApi.Extensions
{
    /// <summary>
    /// 内网宿主专用：把带 [PublicApi] 的 C 端控制器从 MVC 模型里移除。
    /// 约定：[PublicApi] 只归公网宿主 ZR.Front.WebApi（见 Infrastructure/Attribute/PublicApiAttribute.cs）。
    /// 被移除的控制器不会生成路由与 endpoint，也不会出现在 Swagger 中。
    /// </summary>
    public class RemovePublicApiControllerConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            var targets = application.Controllers
                .Where(c => c.ControllerType.IsDefined(typeof(PublicApiAttribute), inherit: true))
                .ToList();

            foreach (var controller in targets)
            {
                application.Controllers.Remove(controller);
            }
        }
    }

    /// <summary>
    /// 内网宿主专用的注册扩展。
    /// </summary>
    public static class PublicApiExclusionExtension
    {
        /// <summary>
        /// 不注册任何带 [PublicApi] 的 C 端控制器。
        /// </summary>
        public static IServiceCollection ExcludePublicApiControllers(this IServiceCollection services)
        {
            services.Configure<MvcOptions>(o => o.Conventions.Add(new RemovePublicApiControllerConvention()));
            return services;
        }
    }
}
