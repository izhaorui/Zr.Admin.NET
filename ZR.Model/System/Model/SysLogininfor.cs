namespace ZR.Model.System
{
    /// <summary>
    /// sys_logininfor 表
    /// </summary>
    [SugarTable("sys_logininfor", "登录日志表")]
    [Tenant("0")]
    public class SysLogininfor
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long InfoId { get; set; }

        /// <summary>
        /// 用户账号
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 登录状态 0成功 1失败
        /// </summary>
        [SugarColumn(Length = 1, DefaultValue = "0")]
        public string Status { get; set; }

        /// <summary>
        /// 登录IP地址
        /// </summary>
        public string Ipaddr { get; set; } = string.Empty;

        /// <summary>
        /// 登录地点
        /// </summary>
        public string LoginLocation { get; set; } = string.Empty;

        /// <summary>
        /// 浏览器类型
        /// </summary>
        public string Browser { get; set; } = string.Empty;

        /// <summary>
        /// 操作系统
        /// </summary>
        public string Os { get; set; } = string.Empty;

        /// <summary>
        /// 提示消息
        /// </summary>
        public string Msg { get; set; } = string.Empty;

        /// <summary>
        /// 访问时间
        /// </summary>
        public DateTime LoginTime { get; set; } = DateTime.Now;
        [SugarColumn(IsIgnore = true)]
        public DateTime? BeginTime { get; set; }
        [SugarColumn(IsIgnore = true)]
        public DateTime? EndTime { get; set; }
        /// <summary>
        /// 客户端id
        /// </summary>
        public string ClientId { get; set; }
        public long UserId { get; set; }

        /// <summary>
        /// 第一步凭据类型（英文码，前端映射中文）：<see cref="LoginAuthType"/>。
        /// 回答"这次会话是用什么证明身份的"——审计时首先要看的就是它。
        /// </summary>
        [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "第一步凭据类型")]
        public string AuthType { get; set; }

        /// <summary>
        /// 第二因子结果（英文码）：<see cref="LoginTwoFactorType"/>。
        ///
        /// 只有 Status=0（成功）时才有意义。为什么必须单独记：
        /// 恢复码登录与可信设备登录在日志里**原本和普通登录一模一样**（Msg 都是"登录成功"），
        /// 而这两者恰好是最该被审计的两类事件——恢复码使用常意味着认证器丢失/被窃，
        /// 可信设备则说明本次**根本没有校验动态码**。
        /// </summary>
        [SugarColumn(Length = 32, IsNullable = true, ColumnDescription = "第二因子结果")]
        public string TwoFactorType { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string Position
        {
            get
            {
                var temp_location = LoginLocation.Split("-")?[0];
                if (temp_location == "0")
                {
                    return "IP未知";
                }
                return temp_location;
            }
        }
    }

    /// <summary>
    /// 登录日志的第一步凭据类型（存稳定英文码，前端映射为中文标签）。
    ///
    /// 用英文码而不是直接存中文：便于统计/筛选，也不受后续文案调整影响。
    /// 不建字典表是刻意的取舍——这类"系统内部枚举"变更频率极低，
    /// 走字典要多一段种子 SQL 且各租户库都要同步，收益不划算。
    /// </summary>
    public static class LoginAuthType
    {
        /// <summary>账号 + 密码</summary>
        public const string Password = "password";

        /// <summary>手机号 + 短信验证码</summary>
        public const string SmsCode = "sms_code";

        /// <summary>
        /// 第三方授权登录。实际落库为 "oauth:渠道"，如 oauth:wechat_enterprise。
        /// 带渠道是因为"用微信进"和"用钉钉进"在安全审计上是两件事，只看 oauth 无法追责。
        /// </summary>
        public const string OAuth = "oauth";

        /// <summary>拼接第三方登录的凭据类型码（列长 32，渠道名需控制长度）</summary>
        public static string ForOAuth(string authSource)
        {
            return $"{OAuth}:{authSource?.ToLowerInvariant()}";
        }
    }

    /// <summary>
    /// 登录日志的第二因子结果（存稳定英文码，前端映射为中文标签）
    /// </summary>
    public static class LoginTwoFactorType
    {
        /// <summary>该账号未启用双因子，本次未要求第二因子</summary>
        public const string None = "none";

        /// <summary>认证器动态码（TOTP）</summary>
        public const string Totp = "totp";

        /// <summary>一次性恢复码：认证器丢失/换机的高危信号，需重点审计</summary>
        public const string RecoveryCode = "recovery_code";

        /// <summary>可信设备免动态码：本次**未**校验第二因子</summary>
        public const string TrustedDevice = "trusted_device";

        /// <summary>
        /// WebAuthn / Passkey 通过（安全密钥、指纹、Face ID）。
        /// **预留**：WebAuthn 尚未实现，落地后由第二步写入该值。取值与 <see cref="TwoFactorMethod.WebAuthn"/> 一致。
        /// </summary>
        public const string WebAuthn = "webauthn";
    }
}
