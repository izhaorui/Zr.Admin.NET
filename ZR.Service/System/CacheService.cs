using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Common;

namespace ZR.Service.System
{
    public class CacheService
    {
        #region 用户权限 缓存
        public static List<string> GetUserPerms(string key)
        {
            return (List<string>)CacheHelper.GetCache(key);
        }

        public static void SetUserPerms(string key, object data)
        {
            CacheHelper.SetCache(key, data);
        }
        public static void RemoveUserPerms(string key)
        {
            CacheHelper.Remove(key);
        }
        #endregion
    }
}
