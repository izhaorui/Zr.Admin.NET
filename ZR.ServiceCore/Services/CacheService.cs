using ZR.Common;

namespace ZR.ServiceCore.Services
{
    public class CacheService
    {
        private readonly static string CK_verifyScan = "verifyScan_";
        private readonly static string CK_phoneSmsCode = "phone_sms_code_";
        #region 用户权限 缓存
        public static List<string> GetUserPerms(string key)
        {
            return (List<string>)CacheHelper.GetCache(key);
            //return RedisServer.Cache.Get<List<string>>(key).ToList();
        }

        public static void SetUserPerms(string key, object data)
        {
            CacheHelper.SetCache(key, data);
            //RedisServer.Cache.Set(key, data);
        }
        public static void RemoveUserPerms(string key)
        {
            CacheHelper.Remove(key);
            //RedisServer.Cache.Del(key);
        }
        #endregion

        public static object SetScanLogin(string key, Dictionary<string, object> val)
        {
            var ck = CK_verifyScan + key;
            
            return CacheHelper.SetCache(ck,val , 1);
        }
        public static object GetScanLogin(string key)
        {
            var ck = CK_verifyScan + key;
            return CacheHelper.Get(ck);
        }
        public static void RemoveScanLogin(string key)
        {
            var ck = CK_verifyScan + key;
            CacheHelper.Remove(ck);
        }

        public static void SetLockUser(string key, long val, int time)
        {
            var CK = "lock_user_" + key;

            CacheHelper.SetCache(CK, val, time);
        }

        public static long GetLockUser(string key)
        {
            var CK = "lock_user_" + key;

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
            var ck = CK_phoneSmsCode + key;

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
            var ck = CK_phoneSmsCode + key;
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
            var ck = CK_phoneSmsCode + key;

            CacheHelper.Remove(ck);
        }
    }
}
