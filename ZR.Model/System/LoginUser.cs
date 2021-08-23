using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 登录用户信息存储
    /// </summary>
    public class LoginUser
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        /// <summary>
        /// 角色集合
        /// </summary>
        public List<string> RoleIds { get; set; }
        /// <summary>
        /// 权限集合
        /// </summary>
        public List<string> Permissions{ get; set; }
        public LoginUser()
        {
        }

        public LoginUser(long userId, string userName)
        {
            UserId = userId;
            UserName = userName;
        }
        public LoginUser(long userId, string userName, List<string> roleIds, List<string> permissions)
        {
            UserId = userId;
            UserName = userName;
            RoleIds = roleIds;
            Permissions = permissions;
        }
    }
}
