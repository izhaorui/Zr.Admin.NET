using SqlSugar;

namespace ZR.Model.System
{
    /// <summary>
    /// 用户角色关联表 用户N-1 角色
    /// </summary>
    [SugarTable("sys_user_role")]
    [Tenant("0")]
    public class SysUserRole
    {
        [SugarColumn(ColumnName = "user_id", IsPrimaryKey = true)]
        public long UserId { get; set; }

        [SugarColumn(ColumnName = "role_id", IsPrimaryKey = true)]
        public long RoleId { get; set; }
    }
}
