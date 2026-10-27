using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using ZR.Common.Security;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Security
{
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
    /// 密码策略实现。只负责"规则从哪来"（sys_config + 60 秒进程内缓存），
    /// 具体判定委托给 <see cref="PasswordValidator"/>（纯逻辑，脱离 DI 与数据库，
    /// 可单独单元测试，也可被其他宿主复用）。
    /// <para>
    /// 缓存是为了避免每次改密都查一次主库（改密本身低频，但登录页也读规则描述）。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IPasswordPolicyService), ServiceLifetime = LifeTime.Transient)]
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private const string CacheKey = "CACHE-PASSWORD-POLICY";
        private const int CacheMinutes = 1;

        private readonly ISysConfigService _sysConfigService;

        public PasswordPolicyService(ISysConfigService sysConfigService)
        {
            _sysConfigService = sysConfigService;
        }

        public void ValidatePassword(string password, string userName = null)
        {
            PasswordValidator.Validate(GetPolicy(), password, userName);
        }

        public string GetPolicyDescription()
        {
            return PasswordValidator.Describe(GetPolicy());
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
