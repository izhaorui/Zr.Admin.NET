using Come.CollectiveOAuth.Cache;
using Come.CollectiveOAuth.Config;
using Come.CollectiveOAuth.Models;
using Come.CollectiveOAuth.Request;
using Infrastructure;

namespace ZR.OAuth.Providers
{
    /// <summary>
    /// 把 CollectiveOAuthCore（Come.CollectiveOAuth）适配成本项目的 IOAuthProvider。
    ///
    /// 只覆盖上游可用且协议稳定的平台。钉钉、微信小程序不走这里：
    /// - 钉钉在上游是整段注释的旧协议（sns/getuserinfo_bycode 已废弃），必须按新版 OAuth2 重写；
    /// - 小程序是 code2session，没有授权页也没有 state，用上游的 authorize/login 反而绕远。
    /// </summary>
    public sealed class CollectiveOAuthProvider : IOAuthProvider
    {
        private readonly IAuthRequest _request;
        private readonly OAuthStateCache _stateCache;

        public CollectiveOAuthProvider(string authSource, OAuthClientOptions options, OAuthStateCache stateCache)
        {
            AuthSource = authSource;
            _stateCache = stateCache ?? OAuthStateCache.Default;
            _request = CreateRequest(authSource, new ClientConfig
            {
                clientId = options.ClientId,
                clientSecret = options.ClientSecret,
                redirectUri = options.RedirectUri,
                scope = options.Scope,
                agentId = options.AgentId
            }, _stateCache);
        }

        public string AuthSource { get; }

        public bool SupportsWebAuthorize => true;

        public string BuildAuthorizeUrl(string state, string tenantId)
        {
            // authorize() 内部经 getRealState() 把 state 写入缓存，这里不重复写
            return _request.authorize(string.IsNullOrWhiteSpace(state) ? _stateCache.Issue() : state);
        }

        public Task<ExternalUserInfo> ExchangeAsync(string code, string state, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new CustomException("缺少授权码，请重新发起登录");
            }

            if (!_stateCache.TryConsume(state))
            {
                throw new CustomException("授权已失效或已使用，请重新发起登录");
            }

            AuthResponse response;
            try
            {
                response = _request.login(new AuthCallback { code = code, state = state });
            }
            catch (Exception ex)
            {
                // 上游异常信息可能带 URL（含 client_secret），只记摘要，完整内容进日志
                throw new CustomException("第三方授权失败，请稍后重试") { LogMsg = ex.Message };
            }

            if (!response.ok())
            {
                throw new CustomException($"第三方授权失败：{response.msg}") { LogMsg = $"code={response.code}, msg={response.msg}" };
            }

            if (response.data is not AuthUser authUser)
            {
                throw new CustomException("第三方未返回用户信息，请稍后重试");
            }

            return Task.FromResult(new ExternalUserInfo
            {
                AuthSource = AuthSource,
                // 上游多数平台把 openid 放在 uuid；微信系列的 unionid 在 token 上
                OpenId = authUser.uuid,
                UnionId = authUser.token?.unionId,
                NickName = authUser.nickname,
                Avatar = authUser.avatar,
                Email = authUser.email
            });
        }

        /// <summary>
        /// 渠道 → 上游请求类的映射。上游核心库没有工厂，只能在这里维护。
        /// 新增平台时同时补 <see cref="OAuthSources"/> 常量与本方法分支。
        /// </summary>
        private static IAuthRequest CreateRequest(string authSource, ClientConfig config, IAuthStateCache cache)
        {
            return authSource switch
            {
                OAuthSources.WeChatOpen => new WeChatOpenAuthRequest(config, cache),
                OAuthSources.WeChatMp => new WeChatMpAuthRequest(config, cache),
                OAuthSources.WeChatEnterprise => new WeChatEnterpriseAuthRequest(config, cache),
                OAuthSources.WeChatEnterpriseScan => new WeChatEnterpriseScanAuthRequest(config, cache),
                OAuthSources.Github => new GithubAuthRequest(config, cache),
                OAuthSources.Gitee => new GiteeAuthRequest(config, cache),
                OAuthSources.Gitlab => new GitlabAuthRequest(config, cache),
                OAuthSources.Qq => new QQAuthRequest(config, cache),
                OAuthSources.Weibo => new WeiboAuthRequest(config, cache),
                OAuthSources.Douyin => new DouyinAuthRequest(config, cache),
                OAuthSources.Microsoft => new MicrosoftAuthRequest(config, cache),
                OAuthSources.Google => new GoogleAuthRequest(config, cache),
                OAuthSources.Linkedin => new LinkedInAuthRequest(config, cache),
                OAuthSources.Baidu => new BaiduAuthRequest(config, cache),
                OAuthSources.Huawei => new HuaweiAuthRequest(config, cache),
                _ => throw new CustomException($"渠道 {authSource} 未接入授权库")
            };
        }
    }
}