using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 双因子认证（TOTP + 恢复码 + 可信设备）
    /// </summary>
    public interface ISysTwoFactorService : IBaseService<SysUser2faDevice>
    {
        /// <summary>
        /// 用户是否已启用双因子。**只看 `TwoFactorEnabled` 标记**，不探测密钥能否解密：
        /// 密钥不可用时若返回 false，登录会静默跳过第二因子（fail-open）。
        /// 密钥是否可用请用状态接口的 `SecretUsable` 判断。
        /// </summary>
        bool IsEnabled(SysUser user);

        /// <summary>
        /// 指定用户是否已启用双因子。内部按主键查询并绕开数据权限过滤，
        /// 供"尚未建立登录态/可能被数据范围限制"的链路（如扫码登录）判断使用。
        /// 语义同 <see cref="IsEnabled(SysUser)"/>（只看启用标记，fail-closed 优先）。
        /// </summary>
        bool IsEnabled(long userId);

        /// <summary>
        /// 本次登录是否需要二次验证：已启用且当前设备不在信任期内
        /// </summary>
        /// <param name="user">用户实体（须含 2FA 字段）</param>
        /// <param name="clientId">设备标识</param>
        bool RequiresTwoFactor(SysUser user, string clientId);

        /// <summary>
        /// 生成待绑定密钥与 otpauth 地址。
        /// 密钥先以"未启用"状态落库，校验通过才置为启用，避免用户中途放弃留下半绑定状态。
        /// </summary>
        TwoFactorInitDto Init(long userId, string userName);

        /// <summary>
        /// 校验密码 + 验证码后启用双因子，并返回一次性恢复码（明文仅此一次返回）
        /// </summary>
        TwoFactorEnableResultDto Enable(long userId, string password, string code);

        /// <summary>
        /// 用户自助解绑（需密码 + 验证码/恢复码）
        /// </summary>
        void Disable(long userId, string password, string code);

        /// <summary>
        /// 管理员重置：直接清空双因子配置并吊销全部可信设备（用户无需提供验证码）
        /// </summary>
        void Reset(long userId);

        /// <summary>
        /// 绑定状态
        /// </summary>
        TwoFactorStatusDto GetStatus(long userId);

        /// <summary>
        /// 可信设备列表
        /// </summary>
        List<TwoFactorDeviceDto> GetDevices(long userId);

        /// <summary>
        /// 吊销单个可信设备
        /// </summary>
        bool RemoveDevice(long userId, long deviceId);

        /// <summary>
        /// 吊销全部可信设备
        /// </summary>
        void ClearTrustedDevices(long userId);

        /// <summary>
        /// 记住此设备：写入信任记录并刷新存量记录
        /// </summary>
        void TrustDevice(long userId, string clientId, string deviceName, string loginIp);

        /// <summary>
        /// 校验验证码（认证器 6 位码或一次性恢复码）。
        /// 校验失败/锁定/重放均抛 <c>CustomException</c>，成功时消费掉对应的恢复码或时间步。
        /// </summary>
        void VerifyCodeOrThrow(SysUser user, string code);

        /// <summary>
        /// 按主键获取用户实体（含 2FA 字段，绕开数据权限过滤）。
        /// 供登录第二步这类"尚未建立登录态"的链路按租户库取用户。
        /// </summary>
        SysUser GetUserForAuth(long userId);
    }
}
