namespace ZR.Model.System.Tenant
{
    /// <summary>
    /// 套餐菜单配置（主库存储）
    /// 一个套餐关联一组菜单，租户通过套餐间接获得菜单权限。
    /// </summary>
    [SugarTable("sys_tenant_plan_menu", "套餐菜单配置表")]
    [Tenant("0")]
    public class SysTenantPlanMenu : SysBase, IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 套餐编码
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string PlanCode { get; set; }

        /// <summary>
        /// 菜单ID，关联主库 SysMenu.MenuId
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long MenuId { get; set; }
    }
}
