using Infrastructure;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace ZR.OAuth.Providers
{
    /// <summary>
    /// 微信小程序登录（code2session）。
    ///
    /// 与网页授权渠道的三点差异，直接决定了这里不能复用 CollectiveOAuthProvider：
    /// 1) 没有授权页：端上 uni.login 直接拿到 code，所以 SupportsWebAuthorize = false；
    /// 2) 没有 state 往返：state 由服务端预发（/oauth/state）并绑定租户，回调时一次性消费；
    /// 3) 拿不到昵称头像：基础库 2.27.1 起 getUserProfile 不再返回真实信息，
    ///    只能由前端用「头像昵称填写能力」采集后另行补全。此处留空，不伪造"微信用户"这类假数据。
    /// </summary>
    public sealed class WeChatMiniProvider : IOAuthProvider
    {
        private const string Code2SessionUrl = "https://api.weixin.qq.com/sns/jscode2session";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly OAuthClientOptions _options;
        private readonly OAuthStateCache _stateCache;

        public WeChatMiniProvider(string authSource, OAuthClientOptions options, OAuthStateCache stateCache)
        {
            AuthSource = authSource;
            _options = options;
            _stateCache = stateCache ?? OAuthStateCache.Default;
        }

        public string AuthSource { get; }

        public bool SupportsWebAuthorize => false;

        /// <summary>小程序没有授权页，此方法永远不该被调用</summary>
        public string BuildAuthorizeUrl(string state, string tenantId)
        {
            throw new CustomException("小程序登录无需跳转授权页，请在小程序内调用 uni.login 获取 code");
        }

        public async Task<ExternalUserInfo> ExchangeAsync(string code, string state, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new CustomException("缺少授权码，请重新发起登录");
            }

            if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                throw new CustomException("小程序登录未配置 AppSecret，请联系管理员");
            }

            // 先校验 ctx 归属再消费：用别的渠道的 state 来换 code 属于跨渠道拼接，直接拒
            var ctx = _stateCache.GetContext(state);
            if (ctx == null || !string.Equals(ctx.AuthSource, AuthSource, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("授权已失效，请重新发起登录");
            }

            if (!_stateCache.TryConsume(state))
            {
                throw new CustomException("授权已失效或已使用，请重新发起登录");
            }

            string url = $"{Code2SessionUrl}?appid={Uri.EscapeDataString(_options.ClientId)}" +
                         $"&secret={Uri.EscapeDataString(_options.ClientSecret)}" +
                         $"&js_code={Uri.EscapeDataString(code)}" +
                         "&grant_type=authorization_code";

            string body;
            try
            {
                body = await HttpHelper.HttpGetAsync(url);
            }
            catch (Exception ex)
            {
                // 异常原文可能带完整 URL（含 AppSecret），只记类型
                logger.Error(ex, $"小程序 code2session 请求异常：AuthSource={AuthSource}");
                throw new CustomException("微信服务暂不可用，请稍后重试") { LogMsg = $"code2session 请求异常：{ex.GetType().Name}" };
            }

            Code2SessionResult result;
            try
            {
                result = JsonConvert.DeserializeObject<Code2SessionResult>(body);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"小程序 code2session 响应无法解析：AuthSource={AuthSource}");
                throw new CustomException("微信返回数据异常，请稍后重试");
            }

            if (result == null)
            {
                throw new CustomException("微信返回数据异常，请稍后重试");
            }

            if (result.ErrCode != 0)
            {
                string userMessage = result.ErrCode switch
                {
                    // 40029 code 无效、40163 code 已被使用：绝大多数是重复提交或 code 已过期
                    40029 or 40163 => "登录凭证已失效，请重新登录",
                    45011 => "操作过于频繁，请稍后再试",
                    40226 => "当前微信账号被判定为风险账号，暂时无法登录",
                    -1 => "微信服务繁忙，请稍后重试",
                    _ => "微信登录失败，请稍后重试"
                };
                // errmsg 是微信侧的诊断信息，对用户没意义；errcode 是排查的唯一线索，必须留
                logger.Warn($"小程序 code2session 返回错误：AuthSource={AuthSource}, errcode={result.ErrCode}, errmsg={result.ErrMsg}");
                throw new CustomException(userMessage) { LogMsg = $"code2session 失败：errcode={result.ErrCode}, errmsg={result.ErrMsg}" };
            }

            if (string.IsNullOrWhiteSpace(result.OpenId))
            {
                logger.Warn($"小程序 code2session 未返回 openid：AuthSource={AuthSource}");
                throw new CustomException("微信未返回用户标识，请稍后重试");
            }

            return new ExternalUserInfo
            {
                AuthSource = AuthSource,
                OpenId = result.OpenId,
                UnionId = result.UnionId,
                NickName = null,
                Avatar = null,
                Email = null
            };
        }

        /// <summary>
        /// 刻意不声明 session_key 字段：它只用于解密手机号/用户信息，本项目不用，
        /// 一旦落到模型里就容易被顺手打进日志或返回给前端。
        /// </summary>
        private sealed class Code2SessionResult
        {
            [JsonProperty("openid")]
            public string OpenId { get; set; }

            [JsonProperty("unionid")]
            public string UnionId { get; set; }

            [JsonProperty("errcode")]
            public int ErrCode { get; set; }

            [JsonProperty("errmsg")]
            public string ErrMsg { get; set; }
        }
    }
}