using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Security
{
    /// <summary>
    /// 密码策略快照。规则存 sys_config（sys.account.pwd.*），未配置时用这里的默认值。
    /// </summary>
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

    /// <summary>
    /// 密码策略校验：等保 2.0 安全计算环境「身份鉴别」要求鉴别信息具备复杂度要求。
    /// <para>
    /// 注册 / 管理员重置 / 个人改密三条入口统一走本服务 —— 此前只有注册调用了
    /// <c>Tools.PasswordStrength</c>，且该函数只挡纯数字/纯符号/纯字母，形同虚设；
    /// 重置密码与个人改密则完全不校验。这是测评会直接开不符合项的缺口。
    /// </para>
    /// <para>
    /// 配置读取失败（表缺失 / 库抖动）时回退默认值（fail-open），不能因为策略读不到
    /// 就让登录改密整体不可用。
    /// </para>
    /// </summary>
    public interface IPasswordPolicyService
    {
        /// <summary>
        /// 校验明文密码，不符合策略时抛 <see cref="CustomException"/>。
        /// </summary>
        /// <param name="password">明文密码（必须在 MD5 之前调用）</param>
        /// <param name="userName">用户名，用于"禁止口令包含账号"校验，可为空</param>
        void ValidatePassword(string password, string userName = null);

        /// <summary>
        /// 当前生效规则的描述文案，供前端提示与后台展示。
        /// </summary>
        string GetPolicyDescription();

        /// <summary>
        /// 当前生效的策略快照。
        /// </summary>
        PasswordPolicy GetPolicy();
    }

    /// <summary>
    /// 密码策略实现。规则走 sys_config，读到的值按 60 秒进程内缓存，
    /// 避免每次改密都查一次主库（改密本身是低频操作，但登录页也读规则描述）。
    /// </summary>
    [AppService(ServiceType = typeof(IPasswordPolicyService), ServiceLifetime = LifeTime.Transient)]
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private const string CacheKey = "CACHE-PASSWORD-POLICY";
        private const int CacheMinutes = 1;

        // 常见弱口令：覆盖默认口令、连续字符、键盘序、以及带年份后缀的高频变体。
        // 只做"完全相等"匹配（不做到包含，否则 normal_password 这类合法口令会被误伤）。
        private static readonly HashSet<string> WeakPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "12345678", "123456789", "1234567890", "11111111", "00000000", "88888888",
            "password", "password1", "password123", "passw0rd", "p@ssw0rd", "p@ssword",
            "admin123", "admin888", "admin@123", "administrator", "root1234", "root@123",
            "qwertyui", "qwerty123", "abc12345", "abcd1234", "a1234567", "aa123456",
            "1qaz2wsx", "1q2w3e4r", "qazwsxedc", "zaq12wsx", "iloveyou", "welcome1",
            "monkey123", "dragon123", "sunshine", "princess", "football", "baseball",
            "zradmin", "zradmin123", "zr@123456", "test1234", "test@123", "guest123"
        };

        private readonly ISysConfigService _sysConfigService;

        public PasswordPolicyService(ISysConfigService sysConfigService)
        {
            _sysConfigService = sysConfigService;
        }

        public void ValidatePassword(string password, string userName = null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new CustomException("密码不能为空");
            }

            var policy = GetPolicy();

            if (password.Length < policy.MinLength)
            {
                throw new CustomException($"密码长度不能少于 {policy.MinLength} 位");
            }
            if (policy.MaxLength > 0 && password.Length > policy.MaxLength)
            {
                throw new CustomException($"密码长度不能超过 {policy.MaxLength} 位");
            }

            var hasDigit = password.Any(char.IsDigit);
            var hasLower = password.Any(char.IsLower);
            var hasUpper = password.Any(char.IsUpper);
            var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

            if (policy.RequireDigit && !hasDigit)
            {
                throw new CustomException("密码必须包含数字");
            }
            if (policy.RequireLower && !hasLower)
            {
                throw new CustomException("密码必须包含小写字母");
            }
            if (policy.RequireUpper && !hasUpper)
            {
                throw new CustomException("密码必须包含大写字母");
            }
            if (policy.RequireSymbol && !hasSymbol)
            {
                throw new CustomException("密码必须包含特殊字符");
            }

            if (policy.ForbidWeak && WeakPasswords.Contains(password))
            {
                throw new CustomException("密码过于简单，请更换更复杂的密码");
            }

            // 账号名长度 >= 3 才做包含校验：过短的账号（如 "a"）会让大量正常口令被误判
            if (policy.ForbidUsername && !string.IsNullOrWhiteSpace(userName) && userName.Length >= 3)
            {
                if (password.Contains(userName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CustomException("密码不能包含账号名");
                }
            }
        }

        public string GetPolicyDescription()
        {
            var policy = GetPolicy();
            var parts = new List<string> { $"{policy.MinLength}-{policy.MaxLength} 位" };

            var classes = new List<string>();
            if (policy.RequireDigit) classes.Add("数字");
            if (policy.RequireLower) classes.Add("小写字母");
            if (policy.RequireUpper) classes.Add("大写字母");
            if (policy.RequireSymbol) classes.Add("特殊字符");
            if (classes.Count > 0)
            {
                parts.Add("至少包含" + string.Join("、", classes));
            }
            if (policy.ForbidWeak) parts.Add("不得使用常见弱口令");
            if (policy.ForbidUsername) parts.Add("不得包含账号名");

            return string.Join("；", parts);
        }

        public PasswordPolicy GetPolicy()
        {
            var cached = CacheStore.Default.Get<PasswordPolicy>(CacheKey);
            if (cached != null) return cached;

            var policy = LoadPolicy();
            CacheStore.Default.Set(CacheKey, policy, CacheMinutes);
            return policy;
        }

        /// <summary>
        /// 从 sys_config 读取规则。任何一项缺失或非法都回退默认值，
        /// 保证"没配过"与"配错了"都不会让改密入口不可用。
        /// </summary>
        private PasswordPolicy LoadPolicy()
        {
            var policy = new PasswordPolicy();

            try
            {
                int ReadInt(string key, int defaultValue)
                {
                    var value = _sysConfigService.GetSysConfigByKey(key)?.ConfigValue;
                    return int.TryParse(value, out var parsed) ? parsed : defaultValue;
                }

                bool ReadBool(string key, bool defaultValue)
                {
                    var value = _sysConfigService.GetSysConfigByKey(key)?.ConfigValue;
                    return value switch
                    {
                        "1" or "true" or "True" => true,
                        "0" or "false" or "False" => false,
                        _ => defaultValue
                    };
                }

                policy.MinLength = ReadInt("sys.account.pwd.minLength", policy.MinLength);
                policy.MaxLength = ReadInt("sys.account.pwd.maxLength", policy.MaxLength);
                policy.RequireDigit = ReadBool("sys.account.pwd.requireDigit", policy.RequireDigit);
                policy.RequireLower = ReadBool("sys.account.pwd.requireLower", policy.RequireLower);
                policy.RequireUpper = ReadBool("sys.account.pwd.requireUpper", policy.RequireUpper);
                policy.RequireSymbol = ReadBool("sys.account.pwd.requireSymbol", policy.RequireSymbol);
                policy.ForbidUsername = ReadBool("sys.account.pwd.forbidUsername", policy.ForbidUsername);
                policy.ForbidWeak = ReadBool("sys.account.pwd.forbidWeak", policy.ForbidWeak);

                // 配置写反时纠偏：最小长度不得小于 6、不得超过最大长度
                if (policy.MinLength < 6) policy.MinLength = 6;
                if (policy.MaxLength < policy.MinLength) policy.MaxLength = policy.MinLength;
            }
            catch (Exception ex)
            {
                // 主库不可用/表缺失：回退默认规则，不让策略查询把改密链路带崩
                NLog.LogManager.GetCurrentClassLogger().Warn(ex, "读取密码策略配置失败，已回退默认规则");
                return new PasswordPolicy();
            }

            return policy;
        }
    }
}
