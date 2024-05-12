namespace ZR.Model.System
{
    /// <summary>
    /// 用户角色关联表 用户N-1 角色
    /// </summary>
    [SugarTable("sys_user_role", "用户和角色关联表")]
    [Tenant("0")]
    public class SysUserRole : SysBase
    {
        [SugarColumn(ColumnName = "user_id", IsPrimaryKey = true)]
        public long UserId { get; set; }

        [SugarColumn(ColumnName = "role_id", IsPrimaryKey = true)]
        public long RoleId { get; set; }
    }
}
