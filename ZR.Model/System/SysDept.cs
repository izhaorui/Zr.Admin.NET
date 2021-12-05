using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 部门表
    /// </summary>
    [SugarTable("sys_dept")]
    [Tenant("0")]
    public class SysDept: SysBase
    {
        /** 部门ID */
        [SqlSugar.SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long DeptId { get; set; }

        /** 父部门ID */
        public long ParentId { get; set; }

        /** 祖级列表 */
        public string Ancestors { get; set; }

        /** 部门名称 */
        public string DeptName { get; set; }

        /** 显示顺序 */
        public int OrderNum { get; set; }

        /** 负责人 */
        public string Leader { get; set; }

        /** 联系电话 */
        public string Phone { get; set; }

        /** 邮箱 */
        public string Email { get; set; }

        /** 部门状态:0正常,1停用 */
        public string Status { get; set; }

        /// <summary>
        /// 删除标志（0代表存在 2代表删除）
        /// </summary>
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        public string DelFlag { get; set; }

        /** 父部门名称 */
        //[SugarColumn(IsIgnore = true)]
        //public string ParentName { get; set; }

        /// <summary>
        /// 子菜单
        /// </summary>
        public List<SysDept> children = new List<SysDept>();
    }
}
