namespace ZR.OAuth
{
    /// <summary>
    /// 第三方身份的统一模型。
    ///
    /// 业务层（绑定、建号、登录桥接）只依赖本类型，不依赖 Come.CollectiveOAuth 的任何模型，
    /// 这样将来替换/升级授权库时，改动被限制在 IOAuthProvider 的具体实现里。
    /// </summary>
    public sealed class ExternalUserInfo
    {
        /// <summary>渠道标识，统一大写，如 WECHAT_ENTERPRISE</summary>
        public string AuthSource { get; set; }

        /// <summary>
        /// 第三方平台内的唯一标识：微信 openid、企业微信 UserId、钉钉 openId。
        /// 同一主体在不同应用下不同，不能跨应用识别同一个人。
        /// </summary>
        public string OpenId { get; set; }

        /// <summary>
        /// 同一开放平台主体下的统一标识：微信 unionid、钉钉 unionId。
        /// 有值时优先用它做绑定键，否则换一个应用就会多出一批重复账号。
        /// </summary>
        public string UnionId { get; set; }

        public string NickName { get; set; }

        public string Avatar { get; set; }

        public string Email { get; set; }

        /// <summary>
        /// 绑定用的业务唯一键：优先 UnionId，回落 OpenId。
        /// 落库时必须与 AuthSource 一起做唯一索引，不同渠道的 ID 可能撞车。
        /// </summary>
        [JsonIgnore]
        public string UnionKey => string.IsNullOrWhiteSpace(UnionId) ? OpenId : UnionId;
    }
}