using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    [SqlSugar.SugarTable("sys_role_dept")]
    [SqlSugar.Tenant(0)]
    public class SysRoleDept
    {
        public long RoleId { get; set; }
        public long DeptId { get; set; }
    }
}
