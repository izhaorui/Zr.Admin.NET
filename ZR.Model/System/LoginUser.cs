using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 登录用户信息存储
    /// </summary>
    public class LoginUser
    {
        public long UserId { get; set; }
        public long DeptId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        /// <summary>
        /// 角色集合
        /// </summary>
        public List<string> RoleIds { get; set; }
        /// <summary>
        /// 角色集合
        /// </summary>
        public List<SysRole> Roles { get; set; }
        /// <summary>
        /// 权限集合
        /// </summary>
        public List<string> Permissions { get; set; }
        public LoginUser()
        {
        }

        public LoginUser(long userId, string userName, List<string> roleIds, List<string> permissions)
        {
            UserId = userId;
            UserName = userName;
            RoleIds = roleIds;
            Permissions = permissions;
        }
        public LoginUser(SysUser user, List<SysRole> roles, List<string> permissions)
        {
            UserId = user.UserId;
            UserName = user.UserName;
            DeptId = user.DeptId;
            Roles = roles;
            RoleIds = roles.Select(f => f.RoleKey).ToList();
            Permissions = permissions;
        }
    }
}
