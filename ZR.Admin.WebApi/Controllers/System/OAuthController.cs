using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Extensions;
using ZR.Model.System.Dto;
using ZR.ServiceCore.OAuth;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 第三方授权登录（OAuth2 网页授权 / App 端 code 登录）。
    ///
    /// 两条回调形态刻意分开：
    /// - 浏览器：平台回调到 /oauth/callback，后端只把 code+state 302 给前端页面，
    ///   由前端 POST /oauth/login 换 token —— token 绝不进 URL（否则会落到浏览器历史、Referer、网关日志）。
    /// - App/小程序：端上自己拿 code，POST /oauth/login 走同一个出口。
    ///
    /// 所有登录分支都经由 <see cref="ISysThirdAccountService.LoginByCode"/> →
    /// <see cref="ISysLoginService.SignIn"/>，双因子、单会话、设备登记、登录审计与账号密码登录完全一致。
    /// </summary>
    [ApiExplorerSettings(GroupName = "sys")]
    public class OAuthController : BaseController
    {
        private readonly ISysThirdAccountService thirdAccountService;

        public OAuthController(ISysThirdAccountService thirdAccountService)
        {
            this.thirdAccountService = thirdAccountService;
        }

        /// <summary>
        /// 获取授权跳转地址（登录场景）。前端拿到 url 后自行跳转，便于展示错误。
        /// </summary>
        [HttpGet("oauth/authorize")]
        [AllowAnonymous]
        [Log(Title = "第三方登录授权", IsSaveRequestData = false)]
        public IActionResult Authorize([FromQuery] ThirdAuthorizeDto dto)
        {
            if (dto == null || dto.AuthSource.IsEmpty())
            {
                throw new CustomException("缺少 authSource 参数");
            }

            // 未显式指定租户时按子域名解析，与登录页品牌化同一口径
            if (dto.TenantId.IsEmpty())
            {
                dto.TenantId = App.GetCurrentTenantId();
            }

            return SUCCESS(new { url = thirdAccountService.BuildAuthorizeUrl(dto) });
        }

        /// <summary>
        /// 获取绑定用的授权地址（已登录用户）。与登录场景隔离在 state 里，不能互换使用。
        /// </summary>
        [HttpGet("oauth/bind/authorize")]
        [Log(Title = "第三方绑定授权", IsSaveRequestData = false)]
        public IActionResult BindAuthorize([FromQuery] ThirdAuthorizeDto dto)
        {
            if (dto == null || dto.AuthSource.IsEmpty())
            {
                throw new CustomException("缺少 authSource 参数");
            }

            if (dto.TenantId.IsEmpty())
            {
                dto.TenantId = App.GetCurrentTenantId();
            }

            long userId = HttpContext.GetUId();
            return SUCCESS(new { url = thirdAccountService.BuildAuthorizeUrl(dto, userId) });
        }

        /// <summary>
        /// 平台回调入口（仅浏览器场景）。这里不换 token，只把参数转交给前端页面。
        ///
        /// 四个参数都必须可空：成功授权只带 code+state，用户拒绝授权只带 error+error_description。
        /// </summary>
        [HttpGet("oauth/callback")]
        [AllowAnonymous]
        public IActionResult Callback([FromQuery] string? code = null, [FromQuery] string? state = null,
            [FromQuery] string? error = null, [FromQuery] string? error_description = null)
        {
            if (code.IsEmpty() && error.IsEmpty())
            {
                throw new CustomException("回调缺少 code");
            }

            var settings = OAuthSettings.Current;
            // 场景/渠道/租户只能来自 state 载荷（服务端签发、不可篡改）。
            // SaaS 下后端若共用域名，中间件按 Host 定位不到租户，state 是唯一可靠来源。
            var ctx = new OAuthStateCache(settings.StateMinutes).GetContext(state);

            var query = new List<string>();
            if (!code.IsEmpty())
            {
                query.Add($"code={Uri.EscapeDataString(code!)}");
            }
            if (!state.IsEmpty())
            {
                query.Add($"state={Uri.EscapeDataString(state!)}");
            }
            // 回调页要区分登录/绑定：绑定场景的 state 拿去调 /oauth/login 会被拒，
            // 靠前端 sessionStorage 记又会在新标签页里丢失，所以由后端按 state 载荷透传场景与渠道
            if (ctx != null && !ctx.Scene.IsEmpty())
            {
                query.Add($"scene={Uri.EscapeDataString(ctx.Scene)}");
            }
            if (ctx != null && !ctx.AuthSource.IsEmpty())
            {
                query.Add($"authSource={Uri.EscapeDataString(ctx.AuthSource)}");
            }
            // 透传 state 里的租户：回调页用它作为 /oauth/login 的 tenantId 头。
            // 后端服务实例在构造时按该头选库，SaaS 共用后端域名时不给就会出现
            // "授权成功却查不到绑定/表不存在"。值来自服务端 state，不是请求参数。
            if (ctx != null && !ctx.TenantId.IsEmpty())
            {
                query.Add($"tenantId={Uri.EscapeDataString(ctx.TenantId)}");
            }
            // 用户点了"拒绝授权"时平台带 error 回来，原样透传由前端统一提示
            if (!error.IsEmpty())
            {
                query.Add($"error={Uri.EscapeDataString(error!)}");
            }
            if (!error_description.IsEmpty())
            {
                query.Add($"error_description={Uri.EscapeDataString(error_description!)}");
            }

            // 目标地址按租户解析（显式配置 → 租户绑定域名 → 全局配置），
            // 全部来自配置/主库数据，取请求参数就是开放重定向
            string frontendUrl = thirdAccountService.ResolveFrontendCallbackUrl(ctx?.TenantId);
            if (frontendUrl.IsEmpty())
            {
                throw new CustomException("未配置 OAuth:FrontendCallbackUrl，且该租户未绑定域名，无法完成浏览器回调");
            }

            string separator = frontendUrl.Contains('?') ? "&" : "?";
            return Redirect($"{frontendUrl}{separator}{string.Join("&", query)}");
        }

        /// <summary>
        /// App/小程序预发 state。端上拿到 code 后与 state 一并回调 /oauth/login。
        /// </summary>
        [HttpPost("oauth/state")]
        [AllowAnonymous]
        [Log(Title = "第三方预发state", IsSaveRequestData = false)]
        public IActionResult IssueState([FromBody] ThirdStateDto dto)
        {
            if (dto == null || dto.AuthSource.IsEmpty())
            {
                throw new CustomException("缺少 authSource 参数");
            }

            if (dto.TenantId.IsEmpty())
            {
                dto.TenantId = App.GetCurrentTenantId();
            }

            string state = thirdAccountService.IssueAppState(dto);
            return SUCCESS(new { state, expireMinutes = OAuthSettings.Current.StateMinutes });
        }

        /// <summary>
        /// 第三方登录：用 code + state 完成身份校验并签发 token。
        /// App 端与浏览器回调页共用本接口。
        /// </summary>
        [HttpPost("oauth/login")]
        [AllowAnonymous]
        [Log(Title = "第三方登录", IsSaveRequestData = false, IsSaveResponseData = false)]
        public async Task<IActionResult> Login([FromBody] ThirdLoginDto dto)
        {
            if (dto == null || dto.Code.IsEmpty() || dto.State.IsEmpty())
            {
                throw new CustomException("请求参数错误");
            }

            dto.LoginIP = HttpContextExtension.GetClientUserIp(HttpContext);
            dto.LoginLocation = HttpContextExtension.GetIpInfo(dto.LoginIP);

            var result = await thirdAccountService.LoginByCode(dto);
            return ToResponse(LoginResponseMapper.Map(result));
        }

        /// <summary>
        /// 绑定第三方账号（已登录）。绑定成功只建立关系，不重签 token。
        /// </summary>
        [HttpPost("oauth/bind")]
        [Log(Title = "绑定第三方账号", IsSaveRequestData = false)]
        public async Task<IActionResult> Bind([FromBody] ThirdBindDto dto)
        {
            if (dto == null || dto.Code.IsEmpty())
            {
                throw new CustomException("请求参数错误");
            }

            // 用户身份取自 token，不取请求体：否则可以拿别人的 code 绑到自己账号上
            long userId = HttpContext.GetUId();
            await thirdAccountService.Bind(dto, userId);
            return SUCCESS(true);
        }

        /// <summary>
        /// 解绑第三方账号（已登录）
        /// </summary>
        [HttpPost("oauth/unbind")]
        [Log(Title = "解绑第三方账号")]
        public IActionResult Unbind([FromQuery] string authSource)
        {
            long userId = HttpContext.GetUId();
            int rows = thirdAccountService.Unbind(authSource, App.GetCurrentTenantId(), userId);
            return rows > 0
                ? SUCCESS(rows)
                : ToResponse(ResultCode.CUSTOM_ERROR, "未找到有效的绑定记录");
        }

        /// <summary>
        /// 当前用户已绑定的第三方账号
        /// </summary>
        [HttpGet("oauth/binds")]
        public IActionResult MyBinds()
        {
            return SUCCESS(thirdAccountService.GetUserBinds(HttpContext.GetUId()));
        }
    }
}