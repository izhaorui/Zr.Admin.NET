namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 一步登录来源，决定第二步的日志文案与后续签发路径
    /// </summary>
    public enum TwoFactorLoginSource
    {
        /// <summary>账号密码登录</summary>
        Account = 1,

        /// <summary>手机号（短信验证码）登录</summary>
        Phone = 2
    }

    /// <summary>
    /// 本次通过校验所用的凭据类型。
    /// 由 <c>VerifyCodeOrThrow</c> 返回，调用方据此标记登录日志并区分处置：
    /// 恢复码属高危信号（认证器丢失/被窃的典型迹象），需要单独告警。
    /// </summary>
    public enum TwoFactorCredential
    {
        /// <summary>认证器动态码（TOTP）</summary>
        Totp = 1,

        /// <summary>一次性恢复码</summary>
        RecoveryCode = 2
    }

    /// <summary>
    /// 双因子登录第二步：校验一次性票据 + TOTP 验证码
    /// </summary>
    public class TwoFactorVerifyDto
    {
        /// <summary>
        /// 第一步登录返回的一次性票据
        /// </summary>
        [Required(ErrorMessage = "票据不能为空")]
        public string Ticket { get; set; }

        /// <summary>
        /// 认证器 6 位验证码，或一次性恢复码
        /// </summary>
        [Required(ErrorMessage = "验证码不能为空")]
        public string Code { get; set; }

        /// <summary>
        /// 是否记住此设备（免二次验证）
        /// </summary>
        public bool RememberDevice { get; set; }

        /// <summary>
        /// 设备标识（前端 fingerprintjs 的 visitorId）
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 租户ID。第二步为独立请求，需按同一租户库校验用户，故必须显式带上（与 /login 一致）。
        /// </summary>
        public string TenantId { get; set; }
    }

    /// <summary>
    /// 一步登录后写入缓存的一次性票据载荷（服务端内部用，不直接返回前端）。
    /// </summary>
    public class TwoFactorTicketPayload
    {
        public long UserId { get; set; }

        public string UserName { get; set; }

        public string NickName { get; set; }

        public string TenantId { get; set; }

        public string ClientId { get; set; }

        public string LoginIP { get; set; }

        public string LoginLocation { get; set; }

        /// <summary>是否来自手机号登录（决定第二步走哪套签发逻辑）</summary>
        public int LoginSource { get; set; }
    }

    /// <summary>
    /// 双因子绑定初始化结果
    /// </summary>
    public class TwoFactorInitDto
    {
        /// <summary>
        /// Base32 密钥（供无法扫码时手动录入）
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// otpauth:// 地址，前端据此本地生成二维码（不占用服务端二维码依赖）
        /// </summary>
        public string OtpauthUri { get; set; }

        /// <summary>
        /// 发行方标识（认证器 App 中显示）
        /// </summary>
        public string Issuer { get; set; }
    }

    /// <summary>
    /// 双因子启用请求
    /// </summary>
    public class TwoFactorEnableDto
    {
        /// <summary>
        /// 当前账号密码（二次校验，防止会话被劫持后直接接管 2FA）
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        /// <summary>
        /// 认证器 6 位验证码（校验通过才真正启用）
        /// </summary>
        [Required(ErrorMessage = "验证码不能为空")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 双因子解绑/管理员重置请求
    /// </summary>
    public class TwoFactorDisableDto
    {
        /// <summary>
        /// 当前账号密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        /// <summary>
        /// 认证器验证码或恢复码（已无法验证时允许管理员走重置接口）
        /// </summary>
        [Required(ErrorMessage = "验证码不能为空")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 双因子启用结果（恢复码仅在启用时一次性返回）
    /// </summary>
    public class TwoFactorEnableResultDto
    {
        /// <summary>
        /// 一次性恢复码（明文仅此一次返回，服务端只存哈希）
        /// </summary>
        public List<string> RecoveryCodes { get; set; } = new List<string>();
    }

    /// <summary>
    /// 双因子绑定状态
    /// </summary>
    public class TwoFactorStatusDto
    {
        /// <summary>
        /// 是否已启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 配置是否健康（密钥可解密）。
        /// Enabled=true 且 SecretUsable=false 表示配置异常（加密口令变更/密文损坏）：
        /// 此时登录会被拒绝（fail-closed），需管理员重置后重新绑定。
        /// 未启用时一律为 true —— "没绑过 2FA"不是异常，避免消费方误判。
        /// </summary>
        public bool SecretUsable { get; set; }

        /// <summary>
        /// 绑定时间
        /// </summary>
        public DateTime? BoundAt { get; set; }

        /// <summary>
        /// 剩余可用恢复码数量
        /// </summary>
        public int RecoveryCodeRemain { get; set; }

        /// <summary>
        /// 可信设备数量
        /// </summary>
        public int TrustedDeviceCount { get; set; }

        /// <summary>
        /// 当前可用的第二因子方法（<see cref="TwoFactorMethod"/> 取值）。
        /// 前端按此列表渲染入口，不硬编码方法。
        /// </summary>
        public List<string> Methods { get; set; } = new();

        /// <summary>
        /// 首选方法（登录第二步默认展示哪种方式）
        /// </summary>
        public string PreferredMethod { get; set; }
    }

    /// <summary>
    /// 登录设备列表项。
    /// 注意：返回的是**全部已登记设备**，不只是受信任的 ——
    /// 只看受信任设备会漏掉"攻击者用过但没勾选记住"的痕迹。
    /// </summary>
    public class TwoFactorDeviceDto
    {
        public long Id { get; set; }

        public string DeviceId { get; set; }

        public string DeviceName { get; set; }

        public string LoginIp { get; set; }

        /// <summary>信任截止时间；为 null 表示仅登记、未信任（登录仍需动态码）</summary>
        public DateTime? TrustUntil { get; set; }

        /// <summary>当前是否处于信任期内（前端据此显示"免动态码"标签）</summary>
        public bool Trusted { get; set; }

        public DateTime LastUsedAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
