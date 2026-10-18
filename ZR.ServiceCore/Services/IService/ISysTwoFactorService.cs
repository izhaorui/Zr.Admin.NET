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
        /// 本次登录是否需要二次验证：已启用且当前设备凭据不在信任期内。
        /// 判定只认服务端签发的设备凭据（见 <see cref="RecordLoginDevice"/>），客户端指纹等标识一律无效。
        /// </summary>
        /// <param name="user">用户实体（须含 2FA 字段）</param>
        /// <param name="deviceToken">服务端签发的设备凭据明文（客户端在"记住此设备"后保存并带回）；为空表示新设备</param>
        bool RequiresTwoFactor(SysUser user, string deviceToken);

        /// <summary>
        /// 计算设备凭据的 SHA256 十六进制哈希（空输入返回空串）。
        /// 供登录第一步把"本次携带的凭据"以哈希形式绑定进双因子票据——票据只存哈希不存明文。
        /// </summary>
        string HashDeviceToken(string deviceToken);

        /// <summary>
        /// 该账号当前**可用**的第二因子方法列表（取值见 <see cref="TwoFactorMethod"/>）。
        ///
        /// 由服务端计算、前端只按列表渲染入口：WebAuthn 落地时后端把它加进来即可，前端无需改动。
        /// 未启用双因子时返回空列表。
        /// </summary>
        List<string> GetAvailableMethods(SysUser user);

        /// <summary>
        /// 该账号的首选方法，用于登录第二步默认展示哪种方式。
        /// 库中为空或该值已不可用时回退到 <see cref="TwoFactorMethod.Default"/>。
        /// </summary>
        string GetPreferredMethod(SysUser user);

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
        /// 管理员重置：直接清空双因子配置并清空全部设备信任（用户无需提供验证码）。
        /// 保留设备登记记录，避免重置后用户从常用设备登录被误报为"新设备"。
        /// </summary>
        void Reset(long userId);

        /// <summary>
        /// 绑定状态
        /// </summary>
        TwoFactorStatusDto GetStatus(long userId);

        /// <summary>
        /// 登录设备列表（**全部已登记设备**，不只是受信任的）。
        /// 只列受信任设备会漏掉"攻击者用过但没勾选记住"的痕迹，用户无法据此判断异常。
        /// </summary>
        List<TwoFactorDeviceDto> GetDevices(long userId);

        /// <summary>
        /// 移除单个设备（同时失去信任）。移除后该设备再次登录会重新被判定为"新设备"并再次提醒
        /// </summary>
        bool RemoveDevice(long userId, long deviceId);

        /// <summary>
        /// 清空该账号全部设备的信任期（解绑 2FA / 管理员重置）。
        /// 保留设备登记记录：重置后用户从常用设备登录不会被误报"新设备"，也仍能看到历史设备；
        /// 安全上只需确保"没有任何设备再被信任"。
        /// </summary>
        void ClearDeviceTrust(long userId);

        /// <summary>
        /// 登记本次登录设备，返回是否"新设备"与本次签发的设备凭据（仅 trust=true 时有）。
        ///
        /// 与"记住此设备"解耦：**每次登录都登记**，<paramref name="trust"/> 为 true 时才写入信任期并签发新凭据。
        /// 凭据只能由服务端随机生成：客户端带来的哈希只用于"认出这台设备"，绝不采信、绝不写库；
        /// 勾选"记住"时也不按可伪造的 <paramref name="clientId"/> 归并他人记录。
        /// 既无凭据又无指纹且未勾选"记住"时无法识别设备，既不登记也不告警 ——
        /// 否则每次登录都会被当成"新设备"误报。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="deviceTokenHash">本次登录携带的设备凭据哈希（见 <see cref="HashDeviceToken"/>）；为空表示未携带</param>
        /// <param name="clientId">设备标识（前端指纹 visitorId），仅用于登记记录的展示与审计归并</param>
        /// <param name="deviceName">设备名称（浏览器/OS），展示给用户辨认</param>
        /// <param name="loginIp">本次登录 IP</param>
        /// <param name="trust">是否同时标记为受信任（免二次验证），true 时服务端签发新设备凭据</param>
        DeviceRecordResult RecordLoginDevice(long userId, string deviceTokenHash, string clientId, string deviceName, string loginIp, bool trust = false);

        /// <summary>
        /// 校验验证码（认证器 6 位码或一次性恢复码）。
        /// 校验失败/锁定/重放均抛 <c>CustomException</c>，成功时消费掉对应的恢复码或时间步。
        /// </summary>
        /// <returns>
        /// 本次通过校验所用的凭据类型。调用方据此把鉴权方式写进登录日志；
        /// 恢复码（<see cref="TwoFactorCredential.RecoveryCode"/>）还会在服务内触发一次告警。
        /// </returns>
        TwoFactorCredential VerifyCodeOrThrow(SysUser user, string code);

        /// <summary>
        /// 密码变更（自助改密 / 管理员重置）后的收敛动作：清空该账号全部设备的信任期，
        /// 并在启用 2FA 时站内信告知用户。
        ///
        /// 改密码往往正是因为怀疑密码泄露，而"记住此设备"的信任期（默认 7 天）会让已记住的设备
        /// 在信任期内继续免动态码 —— 不清掉等于改了个寂寞。设备登记记录保留，不会误报"新设备"。
        /// </summary>
        void HandlePasswordChanged(long userId);

        /// <summary>
        /// 按主键获取用户实体（含 2FA 字段，绕开数据权限过滤）。
        /// 供登录第二步这类"尚未建立登录态"的链路按租户库取用户。
        /// </summary>
        SysUser GetUserForAuth(long userId);
    }
}
