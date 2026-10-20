using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services
{
    public interface ISysLoginService : IBaseService<SysLogininfor>
    {
        /// <summary>
        /// 账号密码登录的完整流程：校验账号密码 → 双因子闸门 → 签发 token。
        ///
        /// 需要第二步时**不下发 token**，只返回一次性票据；此时也不写登录日志、不更新最后登录信息，
        /// 由第二步按最终结果落一条记录，避免"密码对了但没完成 2FA"被统计成一次成功登录。
        /// </summary>
        /// <param name="loginBody">登录请求</param>
        /// <param name="location">登录地点：调用方按 IP 解析后传入，避免与审计日志重复解析</param>
        LoginResultDto Login(LoginBodyDto loginBody, string location);

        /// <summary>
        /// 生成异地登录提醒
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentLoginIp"></param>
        /// <returns></returns>
        string GetAbnormalLoginNotice(SysUser user, string currentLoginIp);

        /// <summary>
        /// 手机号登录的完整流程：校验账号状态 → 双因子闸门 → 签发 token（语义同 <see cref="Login"/>）
        /// </summary>
        /// <param name="loginBody">登录请求</param>
        /// <param name="user">按手机号查到的用户实体（签发需要完整实体，不能用精简 DTO）</param>
        /// <param name="location">登录地点</param>
        LoginResultDto PhoneLogin(PhoneLoginDto loginBody, SysUser user, string location);

        /// <summary>
        /// 双因子登录第二步：校验一次性票据 + 验证码（或一次性恢复码）后签发 token。
        ///
        /// 票据**一次性消费**；验证失败且未达失败上限时**归还票据**，允许在剩余有效期内重试
        /// （前端弹框即按"输错可重试"设计）；已达上限（锁定）则不归还，防止靠重试给票据续命。
        /// 租户上下文由票据载荷决定：第二步是独立请求，此时尚不知道该读哪个库。
        /// </summary>
        LoginResultDto VerifyTwoFactor(TwoFactorVerifyDto dto);

        /// <summary>
        /// 签发 token 的统一出口：权限装配、单会话、"本会话已通过双因子"标记、登录设备登记都在这里。
        /// 三条登录路径（账号密码 / 手机号 / 双因子第二步）共用，避免某个入口漏掉其中一步。
        /// </summary>
        /// <param name="clientId">设备标识（前端指纹），仅用于设备列表展示与审计</param>
        /// <param name="deviceTokenHash">本次携带的设备凭据哈希，为空表示新设备</param>
        /// <param name="trustDevice">是否把该设备标记为受信任（用户勾选"记住此设备"），true 时服务端签发新凭据</param>
        LoginResultDto SignIn(SysUser user, string tenantId, string clientId, string deviceTokenHash,
            string loginIp, bool trustDevice = false, string? abnormalNotice = null);

        /// <summary>
        /// 组装"需要双因子验证"的结果：写入一次性票据，返回票据与可用方法列表（**不含 token**）
        /// </summary>
        LoginResultDto BuildTwoFactorResult(SysUser user, string tenantId, string clientId, string deviceTokenHash,
            string loginIp, string location, TwoFactorLoginSource source);
        /// <summary>
        /// 查询操作日志
        /// </summary>
        PagedInfo<SysLogininfor> GetLoginLog(SysLogininfoQueryDto logininfoDto);
        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <param name="tenantId">租户ID，多租户模式下写入对应租户库</param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor, string tenantId = null);

        /// <summary>
        /// 清空登录日志
        /// </summary>
        public void TruncateLogininfo();

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids);

        /// <summary>
        /// 校验账号是否处于登录锁定期，锁定中直接抛出登录异常。
        /// </summary>
        /// <param name="userName">账号标识（密码登录为用户名，短信登录为手机号）</param>
        void CheckLockUser(string userName);

        /// <summary>
        /// 记录一次登录失败：失败次数达到 sys.account.maxRetryCount 时按 sys.account.lockTime 锁定账号。
        /// </summary>
        /// <param name="key">账号标识（密码登录为用户名，短信登录为手机号）</param>
        void RecordLoginFailure(string key);

        /// <summary>
        /// 清除登录失败次数（登录成功或凭据校验通过后调用）
        /// </summary>
        /// <param name="key">账号标识（密码登录为用户名，短信登录为手机号）</param>
        void ClearLoginFailure(string key);
        /// <summary>
        /// 查询登录日志统计
        /// </summary>
        /// <returns></returns>
        List<StatiLoginLogDto> GetStatiLoginlog();

        /// <summary>
        /// 聚合登录日志安全指标（供 AI 安全分析解读，不返回原始日志）
        /// </summary>
        /// <param name="input">时间范围参数</param>
        /// <returns>聚合指标</returns>
        LoginSecurityMetricsDto GetLoginSecurityMetrics(LogAiAnalysisInput input);

        /// <summary>
        /// 按省份聚合登录日志的地域分布（供 AI 图表问答使用，不返回原始日志）。
        /// 地域取自登录时 IP 解析写入的 LoginLocation，按"省"归一化；
        /// 数据权限与登录日志列表一致（非管理员仅统计本人）。
        /// </summary>
        /// <param name="input">时间范围参数</param>
        /// <param name="topN">返回的省份条数上限，其余省份合并为"其他"</param>
        /// <returns>按登录次数降序的省份统计</returns>
        List<LoginRegionStat> GetLoginRegionStats(LogAiAnalysisInput input, int topN = 12);
    }
}
