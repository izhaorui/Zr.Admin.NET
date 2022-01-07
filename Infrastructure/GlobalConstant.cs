using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure
{
    /// <summary>
    /// 全局静态常量
    /// </summary>
    public class GlobalConstant
    {
        /// <summary>
        /// 管理员权限
        /// </summary>
        public static string AdminPerm = "*:*:*";
        /// <summary>
        /// 管理员角色
        /// </summary>
        public static string AdminRole = "admin";
        /// <summary>
        /// 开发版本API映射路径
        /// </summary>
        public static string DevApiProxy = "/dev-api/";
        /// <summary>
        /// 用户权限缓存key
        /// </summary>
        public static string UserPermKEY = "CACHE-USER-PERM";
    }
}
