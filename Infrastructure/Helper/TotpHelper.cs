using Infrastructure.Model;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Helper
{
    /// <summary>
    /// TOTP（RFC 6238）实现与密钥保护工具。
    ///
    /// 刻意不引第三方 OTP 包：HMAC-SHA1 / Base32 / AES-GCM 均为 BCL 能力，
    /// 符合项目"小功能不引大依赖"的约定，也便于后续统一替换为国密算法。
    /// 仅服务后台管理端账号的双因子认证，与 C 端会员体系无关。
    /// </summary>
    public static class TotpHelper
    {
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>验证码位数</summary>
        public const int DefaultDigits = 6;

        /// <summary>时间步长（秒）</summary>
        public const int DefaultPeriod = 30;

        /// <summary>
        /// 允许的时间窗偏移（±1 个周期 = 前后各 30 秒），用于容忍客户端时钟漂移
        /// </summary>
        public const int DefaultWindow = 1;

        /// <summary>AES-GCM 随机数长度</summary>
        private const int GcmNonceSize = 12;

        /// <summary>认证标签长度</summary>
        private const int GcmTagSize = 16;

        #region Base32 与密钥生成

        /// <summary>
        /// 生成随机 TOTP 密钥（Base32 编码，20 字节 = 160 位，与主流认证器默认一致）
        /// </summary>
        public static string GenerateSecret(int byteLength = 20)
        {
            if (byteLength <= 0) byteLength = 20;
            return EncodeBase32(RandomNumberGenerator.GetBytes(byteLength));
        }

        /// <summary>
        /// 字节数组 → Base32（无填充，认证器约定格式）
        /// </summary>
        public static string EncodeBase32(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;

            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bitsLeft = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    sb.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                    bitsLeft -= 5;
                }
            }
            if (bitsLeft > 0)
            {
                sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Base32 → 字节数组。容错处理空格、连字符与补位符，大小写不敏感。
        /// </summary>
        public static byte[] DecodeBase32(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("密钥不能为空", nameof(input));
            }

            var cleaned = input.Trim().Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
            var output = new byte[cleaned.Length * 5 / 8];
            int buffer = 0, bitsLeft = 0, pos = 0;

            foreach (var c in cleaned)
            {
                var index = Base32Alphabet.IndexOf(c);
                if (index < 0)
                {
                    throw new ArgumentException($"非法的 Base32 字符：{c}", nameof(input));
                }
                buffer = (buffer << 5) | index;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    output[pos++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                    bitsLeft -= 8;
                }
            }
            return output;
        }

        #endregion

        #region 验证码计算与校验

        /// <summary>
        /// 计算指定时间点的验证码
        /// </summary>
        public static string ComputeCode(string secret, long unixTimeSeconds, int digits = DefaultDigits, int period = DefaultPeriod)
        {
            var step = unixTimeSeconds / period;
            return ComputeCodeByStep(DecodeBase32(secret), step, digits);
        }

        /// <summary>
        /// 当前时间的验证码（供试运行/自测与文档示例使用）
        /// </summary>
        public static string ComputeCurrentCode(string secret, int digits = DefaultDigits, int period = DefaultPeriod)
        {
            return ComputeCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), digits, period);
        }

        /// <summary>
        /// 校验验证码，命中则返回匹配的时间步（step）。
        ///
        /// 返回 step 而非 bool 是为了让调用方做**重放防护**：同一 step 的验证码只允许成功一次，
        /// 否则攻击者可在 30 秒窗口内重复使用同一串码。
        /// 校验失败返回 null。
        /// </summary>
        public static long? ValidateCode(string secret, string code, int window = DefaultWindow, int digits = DefaultDigits, int period = DefaultPeriod)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return null;

            var normalized = code.Trim();
            if (normalized.Length != digits || !normalized.All(char.IsAsciiDigit)) return null;

            byte[] key;
            try
            {
                key = DecodeBase32(secret);
            }
            catch (ArgumentException)
            {
                return null;
            }

            var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / period;
            for (var offset = -window; offset <= window; offset++)
            {
                var step = currentStep + offset;
                if (step < 0) continue;

                var expected = ComputeCodeByStep(key, step, digits);
                if (FixedTimeEquals(expected, normalized))
                {
                    return step;
                }
            }
            return null;
        }

        private static string ComputeCodeByStep(byte[] key, long step, int digits)
        {
            var counter = BitConverter.GetBytes(step);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);

            byte[] hash;
            using (var hmac = new HMACSHA1(key))
            {
                hash = hmac.ComputeHash(counter);
            }

            // RFC 4226 动态截断
            var offset = hash[^1] & 0x0F;
            var binary = ((hash[offset] & 0x7F) << 24)
                | ((hash[offset + 1] & 0xFF) << 16)
                | ((hash[offset + 2] & 0xFF) << 8)
                | (hash[offset + 3] & 0xFF);

            var modulus = (int)Math.Pow(10, digits);
            return (binary % modulus).ToString().PadLeft(digits, '0');
        }

        /// <summary>
        /// 定长比较，避免通过响应时间侧信道逐位猜测验证码
        /// </summary>
        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.ASCII.GetBytes(left);
            var rightBytes = Encoding.ASCII.GetBytes(right);
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        /// <summary>
        /// 生成认证器可识别的 otpauth:// 地址（前端据此本地生成二维码）
        /// </summary>
        public static string BuildOtpAuthUri(string issuer, string account, string secret,
            int digits = DefaultDigits, int period = DefaultPeriod)
        {
            // 分隔用的冒号必须保持字面量：Uri.EscapeDataString 会把 ":" 编成 %3A，
            // 而部分认证器会因此把整串 "issuer%3Aaccount" 当成账号名，丢掉发行方分组。
            // 约定写法是：发行方与账号各自转义，再用字面冒号拼接。
            var label = $"{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}";
            return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}"
                + $"&algorithm=SHA1&digits={digits}&period={period}";
        }

        #endregion

        #region 恢复码

        /// <summary>
        /// 生成一次性恢复码（去掉 0/O/1/I 等易混淆字符，XXXXX-XXXXX）
        /// </summary>
        public static string GenerateRecoveryCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[10];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
            return $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
        }

        /// <summary>
        /// 恢复码哈希（SHA256 十六进制）。库中只存哈希，明文仅在启用时返回一次。
        /// </summary>
        public static string HashRecoveryCode(string code)
        {
            var normalized = (code ?? string.Empty).Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        }

        #endregion

        #region 密钥保护（AES-GCM）

        /// <summary>
        /// 加密 TOTP 密钥。格式：Base64(nonce[12] || tag[16] || ciphertext)。
        /// 用 AesGcm 而非 ECB/CBC：自带完整性校验，密文被篡改时解密直接失败而不是产出脏密钥。
        /// </summary>
        public static string ProtectSecret(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            var key = ResolveEncryptionKey();
            var nonce = RandomNumberGenerator.GetBytes(GcmNonceSize);
            var plain = Encoding.UTF8.GetBytes(plainText);
            var cipher = new byte[plain.Length];
            var tag = new byte[GcmTagSize];

            using (var aes = new AesGcm(key, GcmTagSize))
            {
                aes.Encrypt(nonce, plain, cipher, tag);
            }

            var payload = new byte[GcmNonceSize + GcmTagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, GcmNonceSize);
            Buffer.BlockCopy(tag, 0, payload, GcmNonceSize, GcmTagSize);
            Buffer.BlockCopy(cipher, 0, payload, GcmNonceSize + GcmTagSize, cipher.Length);
            return Convert.ToBase64String(payload);
        }

        /// <summary>
        /// 解密 TOTP 密钥。口令变更或密文损坏时返回 null。
        ///
        /// ⚠️ 调用方契约：返回 null 表示"密钥不可用"，**不得**据此把账号当作"未启用双因子"
        /// （那会让登录静默跳过第二因子，属 fail-open）。正确做法是 fail-closed：拒绝登录并提示重置。
        /// </summary>
        public static string UnprotectSecret(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return null;

            try
            {
                var payload = Convert.FromBase64String(cipherText);
                if (payload.Length <= GcmNonceSize + GcmTagSize) return null;

                var nonce = payload[..GcmNonceSize];
                var tag = payload[GcmNonceSize..(GcmNonceSize + GcmTagSize)];
                var cipher = payload[(GcmNonceSize + GcmTagSize)..];
                var plain = new byte[cipher.Length];

                using var aes = new AesGcm(ResolveEncryptionKey(), GcmTagSize);
                aes.Decrypt(nonce, cipher, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception)
            {
                // 口令不匹配 / 密文被篡改 / 非法 Base64：一律视为不可用，不向上抛
                return null;
            }
        }

        /// <summary>
        /// 解析加密口令：优先 TwoFactor:SecretKey，缺省回退 JwtSettings:SecretKey。
        /// 两者都缺失时抛异常（宿主启动配置错误，应显式暴露而不是静默降级成弱密钥）。
        /// </summary>
        private static byte[] ResolveEncryptionKey()
        {
            var settings = AppSettings.Bind("TwoFactor", new TwoFactorSettings());
            var source = !string.IsNullOrWhiteSpace(settings?.SecretKey)
                ? settings.SecretKey
                : AppSettings.Bind("JwtSettings", new JwtSettings())?.SecretKey;

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new InvalidOperationException(
                    "双因子密钥加密口令不可用：请配置 TwoFactor:SecretKey 或 JwtSettings:SecretKey");
            }
            return SHA256.HashData(Encoding.UTF8.GetBytes(source));
        }

        #endregion
    }
}
