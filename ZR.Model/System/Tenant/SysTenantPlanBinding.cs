namespace ZR.Model.System.Tenant
{
    /// <summary>
    /// 租户套餐绑定。
    /// </summary>
    [SugarTable("sys_tenant_plan_binding", "租户套餐绑定表")]
    [Tenant("0")]
    public class SysTenantPlanBinding : SysBase, IMainDbEntity
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string TenantId { get; set; }

        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string PlanCode { get; set; }

        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 用户数上限覆盖值。为空则使用套餐默认值。
        /// </summary>
        public int? MaxUsersOverride { get; set; }

        [SugarColumn(DefaultValue = "0")]
        public int DelFlag { get; set; }
    }
}
