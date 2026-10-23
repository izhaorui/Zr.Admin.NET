using Infrastructure;
using ZR.ServiceCore.OAuth.Providers;

namespace ZR.ServiceCore.OAuth
{
    /// <summary>
    /// Provider 工厂：按 authSource 构造实现，并按租户取应用配置。
    ///
    /// 故意不用 DI 注册每个渠道：渠道配置是按租户动态取的，注册成单例会读到错误的 AppId。
    /// 构造成本只是一次对象分配，每次请求新建没有性能问题。
    /// </summary>
    public static class OAuthProviderFactory
    {
        /// <summary>
        /// 创建 Provider。userId/tenantId 都为空表示匿名入口（登录），非空表示绑定入口。
        /// </summary>
        public static IOAuthProvider Create(string authSource, string tenantId, OAuthSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(authSource))
            {
                throw new CustomException("缺少 authSource 参数");
            }

            settings ??= OAuthSettings.Current;
            if (!settings.Enabled)
            {
                throw new CustomException("第三方登录未启用");
            }

            string source = authSource.Trim().ToUpperInvariant();
            OAuthClientOptions options = settings.Resolve(source, tenantId);
            if (options == null || string.IsNullOrWhiteSpace(options.ClientId))
            {
                throw new CustomException($"渠道 {source} 未配置，请联系管理员");
            }

            var stateCache = new OAuthStateCache(settings.StateMinutes);

            switch (source)
            {
                case OAuthSources.DingTalk:
                case OAuthSources.DingTalkScan:
                    // 上游实现被整段注释且协议已废弃，需自行实现新版 OAuth2，见后续 DingTalkProvider
                    throw new CustomException("钉钉登录尚未接入");

                case OAuthSources.WeChatMini:
                    return new WeChatMiniProvider(source, options, stateCache);

                case OAuthSources.AlipayMp:
                    // 上游把 AlipaySDKNet.Standard 标了 PrivateAssets=all，本项目需显式引用后才能走这条分支
                    throw new CustomException("支付宝登录未启用");

                default:
                    return new CollectiveOAuthProvider(source, options, stateCache);
            }
        }
    }
}