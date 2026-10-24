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
        Phone = 2,

        /// <summary>第三方授权登录（微信/企业微信/钉钉等）</summary>
        ThirdAccount = 3
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
        /// **已废弃，服务端忽略**：设备凭据由第一步票据载荷绑定（<see cref="TwoFactorTicketPayload.DeviceTokenHash"/>），
        /// 第二步不允许替换。保留字段仅为兼容仍在发送它的旧客户端。
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

        /// <summary>
        /// 第一步请求携带的设备凭据哈希（可为空＝新设备/未记住的设备）。
        /// 绑定进票据后第二步只能用它登记/续信任，不允许在第二步替换设备。
        /// </summary>
        public string DeviceTokenHash { get; set; }

        /// <summary>
        /// 票据绝对过期时间。归还票据（输错重试）按此计算剩余 TTL，
        /// 反复重试不得延长票据生命周期。
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        public string LoginIP { get; set; }

        public string LoginLocation { get; set; }

        /// <summary>是否来自手机号登录（决定第二步走哪套签发逻辑）</summary>
        public int LoginSource { get; set; }

        /// <summary>
        /// 第一步凭据类型（取值见 LoginAuthType）。第一步写、第二步原样落日志。
        ///
        /// 为什么不按 LoginSource 在第二步现推：三方登录要精确到渠道（oauth:wechat_enterprise），
        /// 现推只能得到"password/短信"这种粗粒度，会把"用微信登录"记成"用密码登录"。
        /// </summary>
        public string AuthType { get; set; }
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
    /// 登录设备登记结果。
    /// </summary>
    public class DeviceRecordResult
    {
        /// <summary>该设备标识首次出现（决定是否触发新设备提醒）</summary>
        public bool IsNewDevice { get; set; }

        /// <summary>
        /// 本次服务端签发的设备凭据明文，仅在勾选"记住此设备"时非空。
        /// 明文只返回这一次，服务端只存哈希；客户端须保存并在下次登录时带回。
        /// </summary>
        public string DeviceToken { get; set; }
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

    #region WebAuthn / Passkey

    /// <summary>
    /// WebAuthn 注册（绑定）前置：需密码二次校验。
    /// 与 TOTP 的 Init 同口径 —— 仅凭有效会话就能新增因子，等于把"会话劫持"升级成"持久接管"。
    /// </summary>
    public class TwoFactorWebAuthnRegisterDto
    {
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        /// <summary>
        /// 设备名称（可选；留空时由服务端按客户端信息生成）
        /// </summary>
        public string DeviceName { get; set; }
    }

    /// <summary>
    /// WebAuthn 注册/认证 options 下发结果。
    /// OptionsJson 是标准 WebAuthn JSON（base64url 编码），前端 JSON.parse 后直接交给 navigator.credentials；
    /// 刻意不在服务端反序列化成对象再序列化 —— 二次序列化会把 base64url 变成普通 base64，浏览器直接拒绝。
    /// </summary>
    public class TwoFactorWebAuthnOptionsDto
    {
        /// <summary>
        /// 挑战标识（base64url）。第二步校验时原样带回，服务端据此定位保存的 challenge。
        /// </summary>
        public string Challenge { get; set; }

        /// <summary>
        /// PublicKeyCredentialCreationOptions / PublicKeyCredentialRequestOptions 的 JSON 字符串
        /// </summary>
        public string OptionsJson { get; set; }
    }

    /// <summary>
    /// WebAuthn 注册校验
    /// </summary>
    public class TwoFactorWebAuthnRegisterVerifyDto
    {
        [Required(ErrorMessage = "挑战标识不能为空")]
        public string Challenge { get; set; }

        /// <summary>
        /// PublicKeyCredential 的 JSON（前端 JSON.stringify(credential) 的结果）
        /// </summary>
        [Required(ErrorMessage = "凭据不能为空")]
        public string Attestation { get; set; }

        public string DeviceName { get; set; }
    }

    /// <summary>
    /// WebAuthn 认证 options 请求（登录第二步，第一步取挑战）
    /// </summary>
    public class TwoFactorWebAuthnOptionsRequestDto
    {
        [Required(ErrorMessage = "票据不能为空")]
        public string Ticket { get; set; }
    }

    /// <summary>
    /// WebAuthn 登录验证（第二步）。
    /// 与 TOTP 的 TwoFactorVerifyDto 分开：断言是结构化 JSON，与 6 位码/恢复码不是同一种输入。
    /// </summary>
    public class TwoFactorWebAuthnVerifyDto
    {
        [Required(ErrorMessage = "票据不能为空")]
        public string Ticket { get; set; }

        [Required(ErrorMessage = "挑战标识不能为空")]
        public string Challenge { get; set; }

        /// <summary>
        /// PublicKeyCredential 的 JSON（前端 JSON.stringify(credential) 的结果）
        /// </summary>
        [Required(ErrorMessage = "凭据不能为空")]
        public string Assertion { get; set; }

        /// <summary>
        /// 是否记住此设备（免二次验证）
        /// </summary>
        public bool RememberDevice { get; set; }

        /// <summary>
        /// 租户ID。第二步为独立请求，需按同一租户库校验用户。
        /// 实际以票据载荷中的租户为准，此字段仅用于与 /login 保持一致的接口形状。
        /// </summary>
        public string TenantId { get; set; }
    }

    /// <summary>
    /// WebAuthn 凭据列表项
    /// </summary>
    public class TwoFactorWebAuthnCredentialDto
    {
        public long Id { get; set; }

        public string DeviceName { get; set; }

        public string AaGuid { get; set; }

        /// <summary>是否可同步备份（同步型 Passkey 换机后无需重新绑定）</summary>
        public bool IsBackupEligible { get; set; }

        /// <summary>是否已同步备份（区分"能同步"与"确实同步了"）</summary>
        public bool IsBackedUp { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastUsedAt { get; set; }
    }

    /// <summary>
    /// 重命名凭据
    /// </summary>
    public class TwoFactorWebAuthnRenameDto
    {
        [Required(ErrorMessage = "设备名称不能为空")]
        public string DeviceName { get; set; }
    }

    /// <summary>
    /// challenge 缓存载荷（服务端内部用，不返回前端）。
    /// 绑定用途与用户：注册 challenge 不能用于认证，反之亦然，否则同一 challenge 可被跨流程重放。
    /// </summary>
    public class WebAuthnChallengePayload
    {
        public long UserId { get; set; }

        public string TenantId { get; set; }

        /// <summary>用途：register / authenticate（取值见 <see cref="WebAuthnChallengeType"/>>）</summary>
        public string Type { get; set; }

        /// <summary>认证流程专用：绑定的二步票据，防止拿 A 登录的 challenge 去完成 B 登录</summary>
        public string Ticket { get; set; }

        /// <summary>
        /// 服务端保存的原始 options JSON（ToJson 结果）。
        /// 校验必须用这份服务端原始数据，绝不能由前端回传的 options 反推 —— 否则 challenge 校验形同虚设。
        /// </summary>
        public string OptionsJson { get; set; }
    }

    /// <summary>
    /// challenge 用途
    /// </summary>
    public static class WebAuthnChallengeType
    {
        /// <summary>注册（绑定新凭据）</summary>
        public const string Register = "register";

        /// <summary>认证（登录第二步）</summary>
        public const string Authenticate = "authenticate";
    }

    #endregion
}
