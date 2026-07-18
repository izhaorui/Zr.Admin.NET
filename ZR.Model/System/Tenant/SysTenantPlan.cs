namespace ZR.Model.System.Tenant
{
    /// <summary>
    /// 套餐定义。
    /// </summary>
    [SugarTable("sys_tenant_plan", "租户套餐表")]
    [Tenant("0")]
    public class SysTenantPlan : SysBase, IMainDbEntity
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string PlanCode { get; set; }

        [SugarColumn(Length = 100, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string PlanName { get; set; }

        /// <summary>
        /// 最大用户数。-1 表示不限制。
        /// </summary>
        [SugarColumn(DefaultValue = "-1")]
        public int MaxUsers { get; set; } = -1;

        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        [SugarColumn(DefaultValue = "0")]
        public int IsDefault { get; set; }

        [SugarColumn(DefaultValue = "99")]
        public int Sort { get; set; } = 99;

        [SugarColumn(DefaultValue = "0")]
        public int DelFlag { get; set; }
    }
}
