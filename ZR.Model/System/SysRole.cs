using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 角色表 sys_role
    /// </summary>
    [SqlSugar.SugarTable("sys_role")]
    public class SysRole : SysBase
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long RoleId { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string RoleName { get; set; }

        /// <summary>
        /// 角色权限
        /// </summary>
        public string RoleKey { get; set; }

        /// <summary>
        /// 角色排序
        /// </summary>
        public int RoleSort { get; set; }

        /// <summary>
        /// 帐号状态（0正常 1停用）
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 删除标志（0代表存在 2代表删除）
        /// </summary>
        [SqlSugar.SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
        public string DelFlag { get; set; }

        /// <summary>
        /// 菜单组
        /// </summary>
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public long[] MenuIds { get; set; }
        /// <summary>
        /// 部门组（数据权限）
        /// </summary>
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public long[] DeptIds { get; set; }

        public SysRole() { }

        public SysRole(long roleId)
        {
            RoleId = roleId;
        }

        public bool IsAdmin()
        {
            return IsAdmin(RoleId);
        }

        public static bool IsAdmin(long roleId)
        {
            return 1 == roleId;
        }
    }
}
