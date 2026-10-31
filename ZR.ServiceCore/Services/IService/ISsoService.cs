using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 对外 SSO 登录授权：只下发用户基本信息，
    /// 不给外部系统签发可调用本系统后台接口的令牌。
    /// </summary>
    public interface ISsoService
    {
        /// <summary>
        /// 授权页预校验：应用可用且回调地址在白名单内
        /// </summary>
        SsoAuthorizeInfoDto ValidateAuthorize(string appId, string redirectUri, string state);

        /// <summary>
        /// 已登录用户换取一次性授权码（60 秒，绑定应用与用户）
        /// </summary>
        SsoCodeResultDto IssueCode(long userId, SsoCodeRequestDto dto);

        /// <summary>
        /// 外部系统服务端用授权码换用户信息
        /// </summary>
        SsoUserInfoDto ExchangeCode(SsoTokenRequestDto dto, string clientIp);
    }
}