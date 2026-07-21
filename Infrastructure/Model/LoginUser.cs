using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Infrastructure.Model
{
    public class LoginUser
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
        /// 角色集合(eg：admin,common)
        /// </summary>
        public List<string> RoleKeys { get; set; } = [];
        /// <summary>
        /// 角色集合(数据权限过滤使用)
        /// </summary>
        public List<Roles> Roles { get; set; }
        /// <summary>
        /// Jwt过期时间
        /// </summary>
        public DateTime ExpireTime { get; set; }
        /// <summary>
        /// 租户ID
        /// </summary>
        public string TenantId { get; set; }
        /// <summary>
        /// 用户所有权限
        /// </summary>
        public List<string> Permissions { get; set; } = [];
        /// <summary>
        /// 登录时预计算：用户可访问的部门 ID 集合（DEPT_CHILD + CUSTOM 的并集）。
        /// 不走 JWT 序列化（部门多时 JSON 过大），改为 DataPermi 内部 ConcurrentDictionary 缓存。
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public List<long> DataScopeDeptIds { get; set; } = [];
        /// <summary>
        /// 登录时预计算：合并后的数据权限等级（取所有角色中最宽松的权限）
        /// </summary>
        public int ScopeType { get; set; }
        public LoginUser()
        {
        }

        public LoginUser(LoginUser info, List<Roles> roles)
        {
            UserId = info.UserId;
            UserName = info.UserName;
            DeptId = info.DeptId;
            Roles = roles;
            NickName = info.NickName;
            RoleKeys = roles.Select(f => f.RoleKey).ToList();
        }

        public bool HasPermission(string permission)
        {
            if (IsAdmin()) return true;
            return Permissions != null && Permissions.Contains(permission);
        }

        /// <summary>
        /// 是否管理员
        /// </summary>
        /// <returns></returns>
        public bool IsAdmin()
        {
            return RoleKeys.Contains(GlobalConstant.AdminRole) || UserId == 1;
        }
    }

    public class Roles
    {
        public long RoleId { get; set; }
        public string RoleKey { get; set; }
        public int DataScope { get; set; }
    }

    public enum DataPermiEnum
    {
        None = 0,
        /// <summary>
        /// 全部数据权限
        /// </summary>
        All = 1,
        /// <summary>
        /// 自定数据权限
        /// </summary>
        CUSTOM = 2,
        /// <summary>
        /// 部门数据权限
        /// </summary>
        DEPT = 3,
        /// <summary>
        /// 部门及以下数据权限
        /// </summary>
        DEPT_CHILD = 4,
        /// <summary>
        /// 仅本人数据权限
        /// </summary>
        SELF = 5
    }

    /// <summary>
    /// 合并后的用户数据权限等级（登录时预计算，取所有角色中最宽松的权限）
    /// </summary>
    public enum MergedScopeType
    {
        None = 0,    // 无角色或无数据权限（非 HTTP 场景回退到此值）
        Self = 1,    // 仅本人
        Dept = 2,    // 本部门
        DeptList = 3,// 指定部门列表（DEPT_CHILD ∪ CUSTOM 并集）
        All = 4      // 全部数据（管理员 或 DataScope=All）
    }
}
