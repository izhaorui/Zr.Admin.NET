using Microsoft.AspNetCore.Mvc.Filters;

//本命名空间暂时先不改，改动比较大2023年9月2日
namespace ZR.Admin.WebApi.Filters
{
    /// <summary>
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    [Obsolete("已移除，改用JwtAuthMiddleware")]
    public class VerifyAttribute : Attribute, IAuthorizationFilter
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        /// <summary>
        /// 只判断token是否正确，不判断权限
        /// 如果需要判断权限的在Action上加上ApiActionPermission属性标识权限类别，ActionPermissionFilter作权限处理
        /// </summary>
        /// <param name="context"></param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            return;
        }
    }
}
