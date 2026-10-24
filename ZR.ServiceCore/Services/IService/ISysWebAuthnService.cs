using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// WebAuthn / Passkey 第二因子（安全密钥、Windows Hello、Touch ID、Face ID、同步型通行密钥）。
    ///
    /// 与 TOTP 的关系：Passkey 是**附加**的第二因子方式，不改变 <c>TwoFactorEnabled</c> 语义；
    /// 恢复码、可信设备、失败锁定全部复用 <see cref="ISysTwoFactorService"/>。
    /// </summary>
    public interface ISysWebAuthnService : IBaseService<SysUserWebAuthnCredential>
    {
        /// <summary>
        /// 功能是否可用：开关打开且已配置 Origins。
        /// 未配置 Origins 时一律返回 false（fail-closed）—— 没有 Origin 校验的 WebAuthn 等于没有防钓鱼。
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 该账号是否已绑定至少一个凭据（决定可用方法列表里是否出现 webauthn）
        /// </summary>
        bool HasCredential(long userId);

        /// <summary>
        /// 取注册（绑定）options。需密码二次校验，防止会话被劫持后直接接管第二因子。
        /// </summary>
        TwoFactorWebAuthnOptionsDto MakeRegisterOptions(long userId, string userName, string displayName, string password);

        /// <summary>
        /// 校验 attestation 并落库。
        /// challenge 由服务端缓存原子消费，注册与认证用途分离，不可混用。
        /// </summary>
        Task<TwoFactorWebAuthnCredentialDto> CompleteRegistrationAsync(long userId, string challenge, string attestationJson, string deviceName);

        /// <summary>
        /// 取认证（登录第二步）options。AllowedCredentials 只含该账号已绑定的凭据。
        /// </summary>
        TwoFactorWebAuthnOptionsDto MakeAssertionOptions(long userId, string ticket);

        /// <summary>
        /// 校验断言并更新签名计数器。
        /// </summary>
        /// <param name="payload">已消费的 challenge 载荷（含用户、用途、票据、原始 options）</param>
        /// <param name="assertionJson">前端 PublicKeyCredential 的 JSON</param>
        /// <returns>本次使用的凭据主键</returns>
        Task<long> VerifyAssertionAsync(WebAuthnChallengePayload payload, string assertionJson);

        /// <summary>
        /// 凭据列表（只返回展示字段，不返回公钥与凭据 ID）
        /// </summary>
        List<TwoFactorWebAuthnCredentialDto> GetCredentials(long userId);

        /// <summary>
        /// 吊销单条凭据。条件带 UserId，防止用他人凭据 Id 越权删除。
        /// </summary>
        bool RemoveCredential(long userId, long credentialId);

        /// <summary>
        /// 重命名凭据（仅展示用）
        /// </summary>
        bool RenameCredential(long userId, long credentialId, string deviceName);

        /// <summary>
        /// 清空该账号全部凭据。
        ///
        /// 解绑 2FA / 管理员重置必须调用：`TwoFactorEnabled` 置 0 只清了 sys_user 的字段，
        /// 凭据在独立表里，不删就会出现"管理员以为已重置、用户仍能用 Passkey 登录"的静默绕过。
        /// 返回删除条数。
        /// </summary>
        int RemoveAllCredentials(long userId);
    }
}
