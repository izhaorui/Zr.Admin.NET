namespace ZR.Model.System
{
    /// <summary>
    /// 租户信息
    /// </summary>
    [SugarTable("sys_tenant", "租户信息表")]
    [Tenant("0")]
    public class SysTenant : SysBase, IMainDbEntity
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 租户标识，对应 dbConfigs[].ConfigId
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string TenantId { get; set; }

        /// <summary>
        /// 租户名称
        /// </summary>
        [SugarColumn(Length = 100)]
        public string TenantName { get; set; }

        /// <summary>
        /// 状态：0正常 1停用
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        /// <summary>
        /// 过期时间，为空表示不过期
        /// </summary>
        public DateTime? ExpireTime { get; set; }

        /// <summary>
        /// 删除标志：0存在 2删除
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int DelFlag { get; set; }
    }
}
