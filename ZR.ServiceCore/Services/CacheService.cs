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

        public static void SetLockUser(string key, long val, int time)
        {
            var CK = BuildTenantKey("lock_user_" + key);

            CacheHelper.SetCache(CK, val, time);
        }

        public static long GetLockUser(string key)
        {
            var CK = BuildTenantKey("lock_user_" + key);

            if (CacheHelper.Get(CK) is long t)
            {
                return t;
            }
            return 0;
        }

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
    }
}
