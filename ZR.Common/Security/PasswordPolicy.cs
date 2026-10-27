namespace ZR.Common.Security
{
    /// <summary>
    /// 密码策略快照。规则存 sys_config（sys.account.pwd.*），未配置时用这里的默认值。
    /// </summary>
    /// <remarks>
    /// 放在 ZR.Common 而非 ZR.ServiceCore：它是纯数据模型，校验逻辑（PasswordValidator）
    /// 与配置加载（PasswordPolicyService）都依赖它，下沉后校验逻辑可脱离 DI 与数据库单独测试。
    /// </remarks>
    public class PasswordPolicy
    {
        /// <summary>最小长度，默认 8</summary>
        public int MinLength { get; set; } = 8;

        /// <summary>最大长度，默认 32</summary>
        public int MaxLength { get; set; } = 32;

        /// <summary>必须包含数字，默认是</summary>
        public bool RequireDigit { get; set; } = true;

        /// <summary>必须包含小写字母，默认是</summary>
        public bool RequireLower { get; set; } = true;

        /// <summary>必须包含大写字母，默认否（避免对企业既有口令习惯冲击过大）</summary>
        public bool RequireUpper { get; set; }

        /// <summary>必须包含特殊字符，默认否（等保只要求"复杂度"，大小写+数字已满足）</summary>
        public bool RequireSymbol { get; set; }

        /// <summary>禁止包含用户名，默认是</summary>
        public bool ForbidUsername { get; set; } = true;

        /// <summary>禁止常见弱口令，默认是</summary>
        public bool ForbidWeak { get; set; } = true;
    }
}
