using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 用户角色关联表 用户N-1 角色
    /// </summary>
    [SqlSugar.SugarTable("sys_user_role")]
    [Tenant("0")]
    public class SysUserRole
    {
        [SqlSugar.SugarColumn(ColumnName = "user_id", IsPrimaryKey = true)]
        public long UserId { get; set; }

        [SqlSugar.SugarColumn(ColumnName = "role_id", IsPrimaryKey = true)]
        public long RoleId { get; set; }
    }
}
