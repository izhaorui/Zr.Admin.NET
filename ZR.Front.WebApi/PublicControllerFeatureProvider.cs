using System.Reflection;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ZR.Front.WebApi
{
    /// <summary>
    /// 公网 C 端控制器白名单（fail-closed）。
    /// 仅带 <see cref="PublicApiAttribute"/> 的控制器才进入本宿主的端点表；
    /// 未标记的控制器（内网后台管理接口）一律不注册 → 在公网宿主上返回 404。
    /// </summary>
    internal class PublicControllerFeatureProvider : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
        {
            return base.IsController(typeInfo)
                && typeInfo.IsDefined(typeof(PublicApiAttribute), inherit: true);
        }
    }
}
