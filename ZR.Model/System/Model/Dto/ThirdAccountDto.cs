namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 发起第三方授权（浏览器跳转）
    /// </summary>
    public class ThirdAuthorizeDto
    {
        /// <summary>渠道标识，如 WECHAT_ENTERPRISE</summary>
        public string AuthSource { get; set; }

        /// <summary>租户</summary>
        public string TenantId { get; set; }

        /// <summary>设备标识（前端指纹），与账号密码登录同一口径，用于 2FA 可信设备判定</summary>
        public string ClientId { get; set; }
    }

    /// <summary>
    /// App/小程序预发 state（端内只有 code，没有授权页跳转）
    /// </summary>
    public class ThirdStateDto
    {
        public string AuthSource { get; set; }

        public string TenantId { get; set; }

        public string ClientId { get; set; }
    }

    /// <summary>
    /// 第三方授权回调/端内登录：用 code 换取身份并完成登录
    /// </summary>
    public class ThirdLoginDto
    {
        public string Code { get; set; }

        /// <summary>
        /// 一次性 state。Web 回调由平台原样带回，App 端由 /oauth/state 预发后带回。
        /// 租户与设备标识都从 state 载荷取，不接受请求体另传（否则可被改成别的租户）。
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// 本次携带的设备凭据（"记住此设备"后保存），仅用于 2FA 免验证判定
        /// </summary>
        public string DeviceToken { get; set; }

        [JsonIgnore]
        public string LoginIP { get; set; }

        [JsonIgnore]
        public string LoginLocation { get; set; }
    }

    /// <summary>
    /// 已登录用户绑定第三方账号
    /// </summary>
    public class ThirdBindDto
    {
        public string AuthSource { get; set; }

        public string Code { get; set; }

        public string State { get; set; }
    }

    /// <summary>
    /// 绑定关系（返回给前端展示，不含第三方原始 ID）
    /// </summary>
    public class ThirdAccountDto
    {
        public long Id { get; set; }

        public string AuthSource { get; set; }

        public string NickName { get; set; }

        public string Avatar { get; set; }

        public DateTime BindTime { get; set; }

        public DateTime? LastLoginTime { get; set; }

        public int Status { get; set; }
    }
}