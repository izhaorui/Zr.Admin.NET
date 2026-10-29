using System.Threading.Tasks;

namespace ZR.OAuth
{
    /// <summary>
    /// 第三方授权渠道的统一抽象。
    ///
    /// 实现分两类：
    /// 1) 标准 OAuth2 网页授权（微信/企微/GitHub…），交给 CollectiveOAuthCore 适配；
    /// 2) 端内免跳转（小程序 code2session、钉钉新版 OAuth2），单独实现。
    ///
    /// 约定：所有实现都只负责「拿到可信的第三方身份」，不查库、不建号、不签 token，
    /// 这些属于 SysThirdAccountService 的职责。
    /// </summary>
    public interface IOAuthProvider
    {
        /// <summary>渠道标识，见 <see cref="OAuthSources"/></summary>
        string AuthSource { get; }

        /// <summary>
        /// 是否需要浏览器跳转授权。
        /// 小程序、App 端 uni.login 场景为 false：只有 code，没有授权页。
        /// </summary>
        bool SupportsWebAuthorize { get; }

        /// <summary>
        /// 生成授权地址（仅 SupportsWebAuthorize = true 时可用）。
        /// 内部会把 state 写入缓存，回调时由 <see cref="ExchangeAsync"/> 一次性消费。
        /// </summary>
        /// <param name="state">state 为空时由实现自行生成</param>
        /// <param name="tenantId">租户，用于取该租户的 AppId/Secret 与回调地址</param>
        string BuildAuthorizeUrl(string state, string tenantId);

        /// <summary>
        /// 用授权码换取第三方身份。
        /// state 必须是一次性且已消费的：并发回调下只允许一个请求成功。
        /// </summary>
        Task<ExternalUserInfo> ExchangeAsync(string code, string state, string tenantId);
    }
}