namespace ZR.Model.System
{
    /// <summary>
    /// WebAuthn / Passkey 凭据（安全密钥、Windows Hello、Touch ID、Face ID、同步型通行密钥）。
    ///
    /// 独立成表而非塞进 sys_user 的 JSON 列：认证时需按 CredentialId 走唯一索引精确查找，
    /// 还需要按用户列举、逐条吊销、按 UserHandle 反查，这些 JSON 列都做不到。
    ///
    /// 安全要点：
    /// 1) 只存公钥，私钥永远在认证器（TPM / Secure Enclave）内，服务端被拖库也无法伪造登录；
    /// 2) SignCount 用于克隆检测，更新时必须带旧值做 CAS，否则并发下克隆凭据可绕过计数校验；
    /// 3) 同步型 Passkey（iCloud 钥匙串 / Google 密码管理器）的 SignCount 可能恒为 0，
    ///    不能因 0 就判定异常，否则会误伤大量正常用户。
    /// </summary>
    [SugarTable("sys_user_webauthn_credential", "WebAuthn 凭据")]
    [Tenant("0")]
    [SugarIndex("uk_webauthn_credential_id", nameof(CredentialId), OrderByType.Asc, true)]
    [SugarIndex("idx_webauthn_user", nameof(UserId), OrderByType.Asc)]
    public class SysUserWebAuthnCredential
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// 凭据ID（base64url）。WebAuthn 规范上限 1023 字节，base64url 后约 1364 字符；
        /// 实际认证器绝大多数在 16~64 字节，列宽 512 已覆盖，超长在服务层直接拒绝（防刷表）。
        /// </summary>
        [SugarColumn(Length = 512, ColumnDescription = "凭据ID(base64url)")]
        public string CredentialId { get; set; }

        /// <summary>
        /// 公钥（COSE Key 的 base64 编码）。ES256 约 140 字符、RS256 约 400 字符，2000 留足余量。
        /// </summary>
        [SugarColumn(Length = 2000, ColumnDescription = "公钥(base64)")]
        public string PublicKey { get; set; }

        /// <summary>
        /// 用户句柄（base64url）。注册时下发给认证器，认证时用于确认凭据归属；
        /// 预留无密码登录（discoverable credential）场景的反查。
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "用户句柄(base64url)")]
        public string UserHandle { get; set; }

        /// <summary>
        /// 签名计数器。认证成功后更新，用于检测认证器被克隆。
        /// </summary>
        [SugarColumn(ColumnDescription = "签名计数器")]
        public long SignCount { get; set; }

        /// <summary>
        /// 认证器型号 GUID（AAGUID），仅用于展示与风控，不参与校验
        /// </summary>
        [SugarColumn(Length = 36, IsNullable = true, ColumnDescription = "认证器型号")]
        public string AaGuid { get; set; }

        /// <summary>
        /// 传输方式（usb / nfc / ble / internal / hybrid），逗号分隔，仅用于展示
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true, ColumnDescription = "传输方式")]
        public string Transports { get; set; }

        /// <summary>
        /// 是否可同步备份（1=可同步，如同步型 Passkey）
        /// </summary>
        [SugarColumn(ColumnDescription = "是否可同步备份")]
        public int IsBackupEligible { get; set; }

        /// <summary>
        /// 是否已同步备份
        /// </summary>
        [SugarColumn(ColumnDescription = "是否已同步备份")]
        public int IsBackedUp { get; set; }

        /// <summary>
        /// 设备名称（用户可改，仅用于展示与吊销时辨认）
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true, ColumnDescription = "设备名称")]
        public string DeviceName { get; set; }

        /// <summary>
        /// 绑定时间
        /// </summary>
        [SugarColumn(ColumnDescription = "绑定时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后使用时间（每次认证成功刷新，便于识别长期未用的凭据）
        /// </summary>
        [SugarColumn(ColumnDescription = "最后使用时间")]
        public DateTime LastUsedAt { get; set; }
    }
}
