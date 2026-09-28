using System;

namespace Infrastructure.Attribute
{
    /// <summary>
    /// 公网 C 端接口标记。
    /// 只有被本特性标记的控制器才会进入公网宿主 <c>ZR.Front.WebApi</c> 的端点表（fail-closed 白名单）：
    /// 未标记的控制器（如后台管理接口）在公网宿主上一律不可达（404）。
    /// 约定：新增 C 端接口必须显式标记，否则公网 404。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class PublicApiAttribute : System.Attribute
    {
    }
}
