using System;
using System.Security.Cryptography;

namespace ZR.Common.Security
{
    /// <summary>
    /// 密码哈希工具（PBKDF2-SHA256，加盐）。
    /// 存储格式：PBKDF2$SHA256$迭代次数$盐(Base64)$哈希(Base64)
    /// 兼容旧版 MD5（32 位十六进制）存量数据，校验成功后可通过 NeedsUpgrade 判断是否需要重写。
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2";
        private const string Algorithm = "SHA256";
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int KeySize = 32;

        /// <summary>
        /// 计算密码哈希。
        /// </summary>
        /// <param name="credential">凭据（已为 MD5 形式，与历史数据库值一致）</param>
        public static string Hash(string credential)
        {
            if (string.IsNullOrEmpty(credential)) throw new ArgumentException("密码不能为空", nameof(credential));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(credential, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Prefix}${Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        /// <summary>
        /// 校验凭据是否与存储值匹配。
        /// </summary>
        public static bool Verify(string credential, string stored)
        {
            if (string.IsNullOrEmpty(credential) || string.IsNullOrEmpty(stored)) return false;

            if (IsLegacyMd5(stored))
            {
                return string.Equals(stored, credential, StringComparison.OrdinalIgnoreCase);
            }

            string[] parts = stored.Split('$');
            if (parts.Length != 5 || parts[0] != Prefix || parts[1] != Algorithm
                || !int.TryParse(parts[2], out int iterations) || iterations <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expected = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(credential, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>
        /// 是否为旧版 MD5 存储值，需要升级为 PBKDF2。
        /// </summary>
        public static bool NeedsUpgrade(string stored)
        {
            return string.IsNullOrEmpty(stored) || IsLegacyMd5(stored);
        }

        private static bool IsLegacyMd5(string stored)
        {
            if (stored.Length != 32) return false;
            foreach (char c in stored)
            {
                if (!Uri.IsHexDigit(c)) return false;
            }
            return true;
        }
    }
}
