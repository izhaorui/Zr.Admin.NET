using Microsoft.AspNetCore.Mvc;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 对外 SSO 登录授权（给外部系统用）。
    /// </summary>
    [Route("sso")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SsoAuthorizeController : BaseController
    {
        private readonly ISsoService ssoService;
        private readonly IConfiguration configuration;

        public SsoAuthorizeController(ISsoService ssoService, IConfiguration configuration)
        {
            this.ssoService = ssoService;
            this.configuration = configuration;
        }

        /// <summary>
        /// 授权入口：校验应用与回调地址后跳转登录页
        /// </summary>
        [HttpGet("authorize")]
        [AllowAnonymous]
        public IActionResult Authorize([FromQuery] string appId, [FromQuery] string redirectUri, [FromQuery] string state)
        {
            var info = ssoService.ValidateAuthorize(appId, redirectUri, state);

            string loginUrl = configuration["SsoSetting:LoginPageUrl"];
            if (string.IsNullOrWhiteSpace(loginUrl))
            {
                // 直接失败，让配置缺失在首次联调就暴露，而不是变成"登录后回不到外部应用"的隐蔽故障。
                throw new CustomException("SSO 登录页地址未配置（SsoSetting:LoginPageUrl），请联系管理员");
            }

            string separator = loginUrl.Contains('?') ? "&" : "?";
            string url = $"{loginUrl}{separator}ssoAppId={Uri.EscapeDataString(info.AppId)}"
                + $"&ssoAppName={Uri.EscapeDataString(info.AppName ?? string.Empty)}"
                + $"&ssoRedirectUri={Uri.EscapeDataString(info.RedirectUri)}";

            if (!string.IsNullOrEmpty(info.State))
            {
                url += $"&state={Uri.EscapeDataString(info.State)}";
            }

            return Redirect(url);
        }

        /// <summary>
        /// 授权页预校验（登录页调用，应用失效时不必展示"XX 请求登录"）
        /// </summary>
        [HttpGet("app/{appId}")]
        [AllowAnonymous]
        public IActionResult GetAppInfo(string appId, [FromQuery] string redirectUri, [FromQuery] string state)
        {
            return SUCCESS(ssoService.ValidateAuthorize(appId, redirectUri, state));
        }

        /// <summary>
        /// 已登录用户换取一次性授权码（60 秒有效）
        /// </summary>
        [HttpPost("code")]
        public IActionResult IssueCode([FromBody] SsoCodeRequestDto parm)
        {
            return SUCCESS(ssoService.IssueCode(HttpContext.GetUId(), parm));
        }

        /// <summary>
        /// 外部系统服务端用授权码换用户基本信息
        /// </summary>
        [HttpPost("token")]
        [AllowAnonymous]
        public IActionResult Token([FromBody] SsoTokenRequestDto parm)
        {
            return SUCCESS(ssoService.ExchangeCode(parm, HttpContext.GetClientUserIp()));
        }
    }
}