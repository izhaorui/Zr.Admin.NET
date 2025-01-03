using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.Model
{
    public class TokenModel
    {
        /// <summary>
        /// 用户id
        /// </summary>
        public long UserId { get; set; }
        /// <summary>
        /// 部门id
        /// </summary>
        public long DeptId { get; set; }
        /// <summary>
        /// 登录用户名
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// 用户昵称
        /// </summary>
        public string NickName { get; set; }
        /// <summary>
        /// 角色集合
        /// </summary>
        public List<string> RoleIds { get; set; }
        /// <summary>
        /// 角色集合(数据权限过滤使用)
        /// </summary>
        public List<Roles> Roles { get; set; }
        /// <summary>
        /// Jwt过期时间
        /// </summary>
        public DateTime ExpireTime { get; set; }
        public TokenModel()
        {
        }

        public TokenModel(TokenModel info, List<Roles> roles)
        {
            UserId = info.UserId;
            UserName = info.UserName;
            DeptId = info.DeptId;
            Roles = roles;
            NickName = info.NickName;
            RoleIds = roles.Select(f => f.RoleKey).ToList();
        }
    }

    public class Roles
    {
        public long RoleId { get; set; }
        public string RoleKey { get; set; }
        public int DataScope { get; set; }
    }
}
