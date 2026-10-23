using Infrastructure;
using Infrastructure.Model;
using ZR.Common;
using Infrastructure.Cache;

namespace ZR.ServiceCore.Services
{
    public class CacheService
    {
        private readonly static string CK_verifyScan = "verifyScan_";
        private readonly static string CK_phoneSmsCode = "phone_sms_code_";

        private static string BuildTenantKey(string key)
        {
            var tenantId = App.GetCurrentTenantId();
            return string.IsNullOrWhiteSpace(tenantId) ? key : $"{tenantId}:{key}";
        }

        #region 用户权限 缓存
        public static List<string> GetUserPerms(string key)
        {
            return (List<string>)CacheHelper.GetCache(BuildTenantKey(key));
        }

        public static void SetUserPerms(string key, object data)
        {
            CacheHelper.SetCache(BuildTenantKey(key), data);
        }
        public static void RemoveUserPerms(string key)
        {
            CacheHelper.Remove(BuildTenantKey(key));
        }
        #endregion

        #region 数据权限部门 ID 缓存（DEPT_CHILD + CUSTOM 并集，按租户+用户，TTL 2h）
        private static readonly string DataScopeDeptIdsPrefix = "CACHE-DATASCOPE-DEPTIDS_";

        public static void SetDataScopeDeptIds(long userId, List<long> ids)
        {
            CacheHelper.SetCache(BuildTenantKey(DataScopeDeptIdsPrefix + userId), ids, 120);
        }

        public static List<long> GetDataScopeDeptIds(long userId)
        {
            return CacheHelper.GetCache<List<long>>(BuildTenantKey(DataScopeDeptIdsPrefix + userId)) ?? [];
        }

        public static void RemoveDataScopeDeptIds(long userId)
        {
            CacheHelper.Remove(BuildTenantKey(DataScopeDeptIdsPrefix + userId));
        }
        #endregion

        #region 单设备登录会话缓存
        private static readonly string UserSessionPrefix = "CACHE-USER-SESSION_";

        /// <summary>
        /// 记录用户当前有效会话ID（单设备登录用）。TTL 与 JWT 过期时间一致。
        /// 底层后端由 CacheStore 按配置自动切换：Redis 走 RedisServer:Session 库，否则本地内存。
        /// </summary>
        public static void SetUserSession(long userId, string sessionId)
        {
            JwtSettings jwtSettings = new();
            AppSettings.Bind("JwtSettings", jwtSettings);
            CacheStore.For(CacheBackend.Session).Set(BuildTenantKey(UserSessionPrefix + userId), sessionId, jwtSettings.Expire);
        }

        /// <summary>
        /// 获取用户当前有效会话ID，未登录/已失效返回 null
        /// </summary>
        public static string GetUserSession(long userId)
        {
            return CacheStore.For(CacheBackend.Session).Get<string>(BuildTenantKey(UserSessionPrefix + userId));
        }

        /// <summary>
        /// 清除用户会话（注销时调用）
        /// </summary>
        public static void RemoveUserSession(long userId)
        {
            CacheStore.For(CacheBackend.Session).Remove(BuildTenantKey(UserSessionPrefix + userId));
        }
        #endregion

        #region 注销吊销（会话黑名单）
        private static readonly string RevokedSessionPrefix = "CACHE-REVOKED-SESSION_";

        /// <summary>
        /// 吊销指定会话：注销后该 token 在剩余有效期内不再通过鉴权。
        /// TTL 取 token 剩余有效期，token 自然过期后键随之回收。
        ///
        /// 键刻意不加租户前缀：sessionId 是随机 GUID，全局唯一；而租户上下文在中间件与控制器两侧不一定一致，
        /// 加前缀可能出现"写入端与校验端 key 不同"导致吊销静默失效（宁可多吊销，不能漏吊销）。
        /// </summary>
        public static void SetSessionRevoked(string sessionId, int expireMinutes)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || expireMinutes <= 0) return;
            CacheStore.For(CacheBackend.Session).Set(RevokedSessionPrefix + sessionId, 1, expireMinutes);
        }

        /// <summary>
        /// 判断会话是否已被注销吊销。
        /// 缓存丢失/后端重启时返回 false（fail-open），退化为"不校验"，避免误踢全体在线用户。
        /// </summary>
        public static bool IsSessionRevoked(string sessionId)
        {
            return !string.IsNullOrWhiteSpace(sessionId)
                && CacheStore.For(CacheBackend.Session).Exists(RevokedSessionPrefix + sessionId);
        }
        #endregion



        public static object SetScanLogin(string key, Dictionary<string, object> val)
        {
            var ck = BuildTenantKey(CK_verifyScan + key);

            return CacheHelper.SetCache(ck, val, 1);
        }
        public static object GetScanLogin(string key)
        {
            var ck = BuildTenantKey(CK_verifyScan + key);
            return CacheHelper.Get(ck);
        }
        public static void RemoveScanLogin(string key)
        {
            var ck = BuildTenantKey(CK_verifyScan + key);
            CacheHelper.Remove(ck);
        }

        #region 登录失败计数与账号锁定
        private static readonly string LoginFailPrefix = "CACHE-LOGIN-FAIL_";
        private static readonly string LoginLockAlertPrefix = "CACHE-LOGIN-LOCK-ALERT_";

        /// <summary>
        /// 记录账号锁定时间点（Unix 秒，到该时间点自动解锁）。
        /// 走 CacheStore：Redis 启用时多实例共享锁定状态，未启用时回退本地内存；
        /// 不能直接用 CacheHelper（进程内缓存），否则多实例部署下每个实例各锁一半、重启即解锁。
        /// </summary>
        public static void SetLockUser(string key, long val, int time)
        {
            var CK = BuildTenantKey("lock_user_" + key);

            CacheStore.For(CacheBackend.Cache).Set(CK, val, time);
        }

        /// <summary>
        /// 读取账号锁定时间点，0 表示未锁定
        /// </summary>
        public static long GetLockUser(string key)
        {
            var CK = BuildTenantKey("lock_user_" + key);

            return CacheStore.For(CacheBackend.Cache).Get<long?>(CK) ?? 0;
        }

        /// <summary>
        /// 登录失败次数 +1，返回累加后的次数。首次写入时设置过期时间，避免失败次数永久累积。
        /// </summary>
        public static long IncreaseLoginFail(string key, int ttlMinutes)
        {
            return CacheStore.For(CacheBackend.Cache).Increment(BuildTenantKey(LoginFailPrefix + key), ttlMinutes);
        }

        /// <summary>
        /// 清除登录失败次数（登录成功后调用）
        /// </summary>
        public static void RemoveLoginFail(string key)
        {
            CacheStore.For(CacheBackend.Cache).Remove(BuildTenantKey(LoginFailPrefix + key));
        }

        /// <summary>
        /// 锁定提醒限频：同一账号一轮锁定期内只提醒一次，防止被反复尝试刷爆系统消息。
        /// </summary>
        public static bool TryMarkLoginLockAlert(string key, int ttlMinutes)
        {
            if (ttlMinutes <= 0) ttlMinutes = 1;

            return CacheStore.For(CacheBackend.Cache).TrySet(BuildTenantKey(LoginLockAlertPrefix + key), 1, ttlMinutes);
        }
        #endregion

        /// <summary>
        /// 缓存手机验证码
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public static object SetPhoneCode(string key, string val)
        {
            var ck = BuildTenantKey(CK_phoneSmsCode + key);

            return CacheHelper.SetCache(ck, val, 10);
        }

        /// <summary>
        /// 校验手机验证码是否正确
        /// </summary>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public static bool CheckPhoneCode(string key, string val)
        {
            var ck = BuildTenantKey(CK_phoneSmsCode + key);
            var save_code = CacheHelper.Get(ck);

            if (save_code != null && save_code.Equals(val))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 缓存手机验证码
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static void RemovePhoneCode(string key)
        {
            var ck = BuildTenantKey(CK_phoneSmsCode + key);

            CacheHelper.Remove(ck);
        }

        #region 会员状态缓存（C 端鉴权状态复核，TTL 60 秒）
        private static readonly string MemberStatusPrefix = "CACHE-MEMBER-STATUS_";

        /// <summary>
        /// 缓存会员可用状态（1=不可用/已禁用或已删除，0=正常）。
        /// 供 MemberAuthorize 状态复核；Redis 未启用时由 CacheStore 自动回退本地内存。
        /// TTL 仅 1 分钟 + SetStatus 主动清除，保证禁用指令快速全端生效。
        /// </summary>
        public static void SetMemberStatus(long memberId, int status)
        {
            CacheStore.For(CacheBackend.Cache).Set(BuildTenantKey(MemberStatusPrefix + memberId), status, 1);
        }

        /// <summary>
        /// 读取缓存的会员可用状态；未命中返回 null（调用方回查库）
        /// </summary>
        public static int? GetMemberStatus(long memberId)
        {
            return CacheStore.For(CacheBackend.Cache).Get<int?>(BuildTenantKey(MemberStatusPrefix + memberId));
        }

        /// <summary>
        /// 清除会员状态缓存（启用/禁用后调用）
        /// </summary>
        public static void RemoveMemberStatus(long memberId)
        {
            CacheStore.For(CacheBackend.Cache).Remove(BuildTenantKey(MemberStatusPrefix + memberId));
        }
        #endregion

        #region 后台用户状态缓存（鉴权状态复核，TTL 60 秒）
        private static readonly string UserStatusPrefix = "CACHE-USER-STATUS_";

        /// <summary>
        /// 缓存后台用户可用状态（1=不可用/已禁用或已删除，0=正常）。
        /// 供 <see cref="Middleware.JwtAuthMiddleware"/> 做状态复核：JWT 无状态，
        /// 仅靠登录时校验 Status 挡不住"先登录后被禁用，存量令牌在有效期内仍可调用"。
        /// 禁用时主动清除并由复核重新回填，多实例下最迟 60 秒全端生效。
        /// </summary>
        public static void SetUserStatus(long userId, int status)
        {
            CacheStore.For(CacheBackend.Cache).Set(BuildTenantKey(UserStatusPrefix + userId), status, 1);
        }

        /// <summary>
        /// 读取缓存的后台用户状态；未命中返回 null（调用方回查库）
        /// </summary>
        public static int? GetUserStatus(long userId)
        {
            return CacheStore.For(CacheBackend.Cache).Get<int?>(BuildTenantKey(UserStatusPrefix + userId));
        }

        /// <summary>
        /// 清除后台用户状态缓存（启用/禁用/删除后调用）
        /// </summary>
        public static void RemoveUserStatus(long userId)
        {
            CacheStore.For(CacheBackend.Cache).Remove(BuildTenantKey(UserStatusPrefix + userId));
        }
        #endregion

        #region 租户可用状态缓存（鉴权状态复核，TTL 60 秒）
        private static readonly string TenantActivePrefix = "CACHE-TENANT-ACTIVE_";

        /// <summary>
        /// 缓存租户可用状态（1=可用，0=已停用/已过期/已删除）。
        /// key 刻意不加当前租户前缀：租户状态是平台级数据，同一租户在任意上下文下只应有一份缓存，
        /// 否则停服清除时只能清掉当前上下文那一份。
        /// </summary>
        public static void SetTenantActive(string tenantId, int active)
        {
            CacheStore.For(CacheBackend.Cache).Set(TenantActivePrefix + tenantId, active, 1);
        }

        /// <summary>
        /// 读取缓存的租户可用状态；未命中返回 null（调用方回查库）
        /// </summary>
        public static int? GetTenantActive(string tenantId)
        {
            return CacheStore.For(CacheBackend.Cache).Get<int?>(TenantActivePrefix + tenantId);
        }

        /// <summary>
        /// 清除租户可用状态缓存（停服/续费/注销后调用）
        /// </summary>
        public static void RemoveTenantActive(string tenantId)
        {
            CacheStore.For(CacheBackend.Cache).Remove(TenantActivePrefix + tenantId);
        }
        #endregion

        #region 双因子认证（TOTP）缓存
        private static readonly string TwoFactorTicketPrefix = "CACHE-2FA-TICKET_";
        private static readonly string TwoFactorStepPrefix = "CACHE-2FA-STEP_";
        private static readonly string TwoFactorFailPrefix = "CACHE-2FA-FAIL_";
        private static readonly string TwoFactorLockPrefix = "CACHE-2FA-LOCK_";
        private static readonly string TwoFactorAlertPrefix = "CACHE-2FA-ALERT_";
        private static readonly string TwoFactorNewDeviceAlertPrefix = "CACHE-2FA-NEWDEVICE_";
        private static readonly string TwoFactorSessionOkPrefix = "CACHE-2FA-SESSIONOK_";

        /// <summary>
        /// 写入"密码已通过、等待 TOTP 校验"的一次性票据。
        /// 走会话库（Session）：与单设备登录会话同生命周期语义，Redis 未启用时自动回退内存。
        /// </summary>
        public static void SetTwoFactorTicket(string ticket, object payload, int minutes)
        {
            CacheStore.For(CacheBackend.Session).Set(TwoFactorTicketPrefix + ticket, payload, minutes);
        }

        /// <summary>
        /// 消费票据：原子地保证同一票据只被一个请求取到，避免并发重放。
        /// 先读票据、存在才以 TrySet 抢占"已消费"标记（Redis SETNX / 内存锁），抢占失败直接返回 null ——
        /// 不存在的票据不落任何缓存状态（防匿名端点灌垃圾键）。
        ///
        /// 票据 key 刻意不加租户前缀：第二步请求到达时租户上下文尚未建立（不知道读哪个库），
        /// 若 key 依赖租户就会永远读不到票据。租户信息由票据载荷携带，消费后再切上下文。
        /// 票据为 32 位随机串 + 5 分钟 TTL + 一次性，无前缀不构成越权风险。
        /// </summary>
        public static T ConsumeTwoFactorTicket<T>(string ticket, int minutes) where T : class
        {
            if (string.IsNullOrEmpty(ticket)) return null;

            var backend = CacheStore.For(CacheBackend.Session);
            var key = TwoFactorTicketPrefix + ticket;

            // 先读票据、再抢"已消费"标记：票据不存在（伪造/随机串/已过期）时不落任何缓存状态，
            // 否则匿名端点可被灌入任意数量的 _used 垃圾键（各带 minutes TTL）。
            // 抢占仍以 TrySet 原子完成，并发提交同一票据只有一个请求能取到（防重放）。
            var payload = backend.Get<T>(key);
            if (payload == null) return null;

            if (!backend.TrySet(TwoFactorTicketPrefix + ticket + "_used", 1, minutes))
            {
                return null;
            }

            backend.Remove(key);
            return payload;
        }

        /// <summary>
        /// 归还票据：验证码输错后允许在票据有效期内重试（前端弹框即按"输错可重试"设计）。
        /// 撤销 <see cref="ConsumeTwoFactorTicket"/> 的一次性消费标记并还原载荷；
        /// 成功路径不调用，票据的"一次性"只在验证通过后最终成立。
        /// 按 <paramref name="expiresAt"/>（首次签发时的绝对过期时间）恢复剩余 TTL ——
        /// 反复重试不刷新票据生命周期；已过期则直接不归还。
        /// 暴力尝试仍受约束：连续失败达阈值按用户锁定，且票据 TTL 到期即失效。
        /// </summary>
        public static void RestoreTwoFactorTicket<T>(string ticket, T payload, DateTime expiresAt) where T : class
        {
            if (string.IsNullOrEmpty(ticket) || payload == null) return;

            var remaining = (int)Math.Ceiling((expiresAt - DateTime.Now).TotalMinutes);
            if (remaining <= 0) return;

            var backend = CacheStore.For(CacheBackend.Session);
            backend.Set(TwoFactorTicketPrefix + ticket, payload, remaining);
            backend.Remove(TwoFactorTicketPrefix + ticket + "_used");
        }

        /// <summary>
        /// 验证码重放防护：同一时间步（step）的验证码只允许通过一次。
        /// 依赖原子"仅当不存在时写入"，并发下只有一个请求能标记成功。
        /// </summary>
        public static bool TryMarkTwoFactorStep(long userId, long step, int ttlMinutes)
        {
            return CacheStore.For(CacheBackend.Cache)
                .TrySet(BuildTenantKey($"{TwoFactorStepPrefix}{userId}_{step}"), 1, ttlMinutes);
        }

        /// <summary>
        /// 告警限频：同一账号在 intervalMinutes 内只允许发出一次"2FA 连续失败"告警。
        ///
        /// 用原子"仅当不存在时写入"实现，多实例部署下也只有一次能抢到 —— 攻击者反复触发锁定时
        /// 不会把用户邮箱刷爆。返回 true 表示本次抢到（应当发告警），false 表示间隔内已发过。
        /// </summary>
        public static bool TryMarkTwoFactorFailAlert(long userId, int intervalMinutes)
        {
            if (intervalMinutes <= 0) intervalMinutes = 60;

            return CacheStore.For(CacheBackend.Cache)
                .TrySet(BuildTenantKey(TwoFactorAlertPrefix + userId), 1, intervalMinutes);
        }

        /// <summary>
        /// 新设备登录提醒的限频：同一账号在 intervalMinutes 内只提醒一次。
        ///
        /// 与"连续失败告警"用**独立 key**（两类告警互不抑制，一个发生不会吞掉另一个）。
        /// 新设备提醒尤其需要限频：无痕模式 / 清缓存会让设备指纹变化，正常用户也可能连续触发。
        /// </summary>
        public static bool TryMarkNewDeviceAlert(long userId, int intervalMinutes)
        {
            if (intervalMinutes <= 0) intervalMinutes = 60;

            return CacheStore.For(CacheBackend.Cache)
                .TrySet(BuildTenantKey(TwoFactorNewDeviceAlertPrefix + userId), 1, intervalMinutes);
        }

        /// <summary>2FA 校验失败次数 +1，返回累加后的次数</summary>
        public static long IncreaseTwoFactorFail(long userId, int ttlMinutes)
        {
            return CacheStore.For(CacheBackend.Cache).Increment(BuildTenantKey(TwoFactorFailPrefix + userId), ttlMinutes);
        }

        /// <summary>清除 2FA 失败计数（校验成功或锁定到期后）</summary>
        public static void ClearTwoFactorFail(long userId)
        {
            CacheStore.For(CacheBackend.Cache).Remove(BuildTenantKey(TwoFactorFailPrefix + userId));
        }

        /// <summary>锁定某用户的 2FA 校验</summary>
        public static void SetTwoFactorLock(long userId, int minutes)
        {
            CacheStore.For(CacheBackend.Cache).Set(BuildTenantKey(TwoFactorLockPrefix + userId), 1, minutes);
        }

        /// <summary>2FA 校验是否处于锁定期</summary>
        public static bool IsTwoFactorLocked(long userId)
        {
            return CacheStore.For(CacheBackend.Cache).Exists(BuildTenantKey(TwoFactorLockPrefix + userId));
        }

        /// <summary>
        /// 标记会话已通过双因子校验。扫码登录用它判断"移动端当前会话确实过了 2FA"，
        /// 避免已启用 2FA 的用户用旧会话（启用前签发的 token）绕过扫码闸门。
        /// TTL 与 JWT 过期一致。
        /// </summary>
        public static void SetTwoFactorSessionOk(long userId, string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            JwtSettings jwtSettings = new();
            AppSettings.Bind("JwtSettings", jwtSettings);
            CacheStore.For(CacheBackend.Session)
                .Set(BuildTenantKey($"{TwoFactorSessionOkPrefix}{userId}_{sessionId}"), 1, jwtSettings.Expire);
        }

        public static bool IsTwoFactorSessionOk(long userId, string sessionId)
        {
            return !string.IsNullOrEmpty(sessionId)
                && CacheStore.For(CacheBackend.Session).Exists(BuildTenantKey($"{TwoFactorSessionOkPrefix}{userId}_{sessionId}"));
        }
        #endregion
    }
}
