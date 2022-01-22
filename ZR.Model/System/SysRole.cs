using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 角色表 sys_role
    /// </summary>
    [SugarTable("sys_role")]
    [Tenant("0")]
    public class SysRole : SysBase
    {
        /// <summary>
        /// 角色ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
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
        [SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
        public string DelFlag { get; set; }
        /// <summary>
        /// 数据范围（1：全部数据权限 2：自定数据权限 3：本部门数据权限 4：本部门及以下数据权限））
        /// </summary>
        public string DataScope { get; set; }
        /// <summary>
        /// 菜单树选择项是否关联显示
        /// </summary>
        [SugarColumn(ColumnName = "menu_check_strictly")]
        public bool MenuCheckStrictly { get; set; }
        /// <summary>
        /// 部门树选择项是否关联显示
        /// </summary>
        [SugarColumn(ColumnName = "dept_check_strictly")]
        public bool DeptCheckStrictly { get; set; }
        /// <summary>
        /// 菜单组
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public long[] MenuIds { get; set; }
        /// <summary>
        /// 部门组（数据权限）
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public long[] DeptIds { get; set; }
        /// <summary>
        /// 用户个数
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public int UserNum { get; set; }

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
