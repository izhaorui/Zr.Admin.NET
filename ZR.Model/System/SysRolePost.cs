using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;

namespace ZR.Model.System
{
    /// <summary>
    /// 角色部门
    /// </summary>
    [SugarTable("sys_role_post")]
    [Tenant("0")]
    public class SysRolePost
    {
        public long RoleId { get; set; }
        public long DeptId { get; set; }
    }
}
