using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ZR.ServiceCore.Member
{
    /// <summary>
    /// 会员鉴权过滤器：校验 mall-token，失败返回业务码 OAUTH_FAIL(201)（HTTP 200）。
    /// 刻意避开 401，防止前端共享拦截器误触发后台"会话过期重新登录"流程；
    /// 与后台 JwtAuthMiddleware / ActionPermissionFilter 完全解耦，对后台链路零改动。
    /// 会员身份为平台级通用能力，置于框架共享层供各业务模块（商城/内容/社交等）的 C 端控制器复用。
    /// </summary>
    public class MemberAuthorizeAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// HttpContext.Items 中会员身份的 Key（供 Controller 读取，避免同请求内重复解析）
        /// </summary>
        public const string MemberContextKey = "__CurrentMember";

        /// <summary>
        /// 读取当前请求的会员身份（由本过滤器写入；未登录返回 null）。
        /// 控制器统一通过此方法取会员身份，避免各自解析令牌。
        /// </summary>
        public static MemberToken GetMember(HttpContext context)
        {
            return context != null && context.Items.TryGetValue(MemberContextKey, out var value)
                ? value as MemberToken
                : null;
        }

        public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var member = MemberJwt.GetMember(context.HttpContext);
            if (member == null || member.MemberId <= 0)
            {
                context.Result = new JsonResult(new ApiResult((int)ResultCode.OAUTH_FAIL, "登录已失效，请重新登录"));
                return Task.CompletedTask;
            }

            context.HttpContext.Items[MemberContextKey] = member;
            return base.OnActionExecutionAsync(context, next);
        }
    }
}
