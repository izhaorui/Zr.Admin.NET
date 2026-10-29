using Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace ZR.Infrastructure.WebExtensions
{
    /// <summary>
    /// DataProtection 密钥环配置。
    ///
    /// 背景：<c>PersistKeysToFileSystem</c> 落盘的是**明文 XML**，里面就是真正的加密密钥。
    /// 文件一旦泄露，受 DataProtection 保护的数据（Cookie、防伪令牌、临时票据等）可被解密或伪造，
    /// 攻击者还能自行签发受保护载荷。因此需要在持久化的基础上再加一层"密钥加密"。
    ///
    /// 加密方式按优先级：
    /// 1. 配置了 DataProtection:CertificatePath → 用 PFX 证书私钥加密（Windows/Linux/Docker 通用，推荐）；
    /// 2. 未配证书且运行在 Windows → DPAPI 本机加密（密钥绑定当前用户/机器，拷贝到别处无法解密）；
    /// 3. 未配证书且非 Windows（Docker 部署的默认情形）→ 只能保持明文，启动时打警告。
    ///
    /// 注意：加密只影响**新生成**的密钥文件；历史明文密钥仍可正常读取，
    /// 所以开启后不会一次性踢掉现有会话。反向操作（去掉加密）同理，旧密钥仍可用。
    /// </summary>
    public static class DataProtectionExtension
    {
        /// <summary>证书文件（PFX，含私钥）路径</summary>
        public const string CertificatePathKey = "DataProtection:CertificatePath";

        /// <summary>证书口令。建议用环境变量 DataProtection__CertificatePassword 提供，不要写进 appsettings</summary>
        public const string CertificatePasswordKey = "DataProtection:CertificatePassword";

        /// <summary>
        /// 注册 DataProtection：持久化密钥环 + 按配置加密密钥。
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="applicationName">应用唯一名。两个宿主的工作目录都是 /app，不显式区分会被当成同一应用共用密钥环</param>
        /// <param name="keyRingPath">密钥环目录</param>
        public static IDataProtectionBuilder AddZRDataProtection(
            this IServiceCollection services,
            IConfiguration configuration,
            string applicationName,
            string keyRingPath)
        {
            var keyDirectory = new DirectoryInfo(keyRingPath);
            if (!keyDirectory.Exists)
            {
                keyDirectory.Create();
            }

            var dpBuilder = services.AddDataProtection()
                .SetApplicationName(applicationName)
                .PersistKeysToFileSystem(keyDirectory);

            var certificatePath = configuration[CertificatePathKey];
            if (!string.IsNullOrWhiteSpace(certificatePath))
            {
                var certificate = LoadCertificate(certificatePath.Trim(), configuration[CertificatePasswordKey]);
                dpBuilder.ProtectKeysWithCertificate(certificate);
            }
            else if (OperatingSystem.IsWindows())
            {
                // 仅 Windows 可用；密钥绑定当前进程身份，换账号/换机器都解不开
                dpBuilder.ProtectKeysWithDpapi();
            }
            else
            {
                Log.WriteLine(ConsoleColor.Yellow,
                    $"警告：DataProtection 密钥环未加密（{keyDirectory.FullName}）。生产环境请配置 {CertificatePathKey} 指向含私钥的 PFX，"
                    + "或至少把该目录放在加密卷上并收紧文件权限（当前容器内是 777）。");
            }

            return dpBuilder;
        }

        /// <summary>
        /// 加载 PFX 证书。EphemeralKeySet：私钥只留在内存，不在 Windows 用户证书存储里留副本。
        /// </summary>
        private static X509Certificate2 LoadCertificate(string path, string password)
        {
            if (!File.Exists(path))
            {
                // 配置了却读不到，属于部署错误：直接失败，避免"以为加密了实际仍是明文"的半开状态
                throw new FileNotFoundException($"DataProtection 证书不存在：{path}", path);
            }

#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
#else
            return new X509Certificate2(path, password, X509KeyStorageFlags.EphemeralKeySet);
#endif
        }
    }
}
