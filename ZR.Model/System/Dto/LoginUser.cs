using System.Linq;

namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 登录用户信息存储
    /// </summary>
    public class LoginUser
    {
        public long UserId { get; set; }
        public long DeptId { get; set; }
        public string UserName { get; set; }
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
        /// <summary>
        /// 权限集合
        /// </summary>
        //public List<string> Permissions { get; set; } = new List<string>();
        public LoginUser()
        {
        }

        public LoginUser(SysUser user, List<Roles> roles)
        {
            UserId = user.UserId;
            UserName = user.UserName;
            DeptId = user.DeptId;
            Roles = roles;
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
