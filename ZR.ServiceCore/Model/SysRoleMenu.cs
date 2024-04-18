namespace ZR.Model.System
{
    /// <summary>
    /// 角色菜单
    /// </summary>
    [SugarTable("sys_role_menu", "角色菜单")]
    [Tenant("0")]
    public class SysRoleMenu : SysBase
    {
        [JsonProperty("roleId")]
        [SugarColumn(IsPrimaryKey = true, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long Role_id { get; set; }
        [JsonProperty("menuId")]
        [SugarColumn(IsPrimaryKey = true, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long Menu_id { get; set; }
    }
}
