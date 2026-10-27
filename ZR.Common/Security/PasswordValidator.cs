using Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ZR.Common.Security
{
    /// <summary>
    /// 密码策略校验（纯逻辑，不做配置读取与缓存）。
    /// <para>
    /// 配置加载见 ZR.ServiceCore 的 PasswordPolicyService。本类只负责"按给定策略判定"，
    /// 因此可以脱离数据库与 DI 直接单元测试，也可被其他宿主（如 C 端注册）复用而不必引用服务层。
    /// </para>
    /// <para>
    /// 等保 2.0 安全计算环境「身份鉴别」要求鉴别信息具备复杂度要求：
    /// 注册 / 管理员重置 / 个人改密三条入口统一走同一套判定，避免各入口规则不一致。
    /// </para>
    /// </summary>
    public static class PasswordValidator
    {
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

        /// <summary>
        /// 校验明文密码，不符合策略时抛 <see cref="CustomException"/>。
        /// </summary>
        /// <param name="policy">当前生效的策略快照</param>
        /// <param name="password">明文密码（必须在 MD5 之前调用）</param>
        /// <param name="userName">用户名，用于"禁止口令包含账号"校验，可为空</param>
        public static void Validate(PasswordPolicy policy, string password, string userName = null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new CustomException("密码不能为空");
            }

            policy ??= new PasswordPolicy();

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

        /// <summary>
        /// 当前规则的描述文案，供前端提示与后台展示。
        /// </summary>
        public static string Describe(PasswordPolicy policy)
        {
            policy ??= new PasswordPolicy();
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
    }
}
