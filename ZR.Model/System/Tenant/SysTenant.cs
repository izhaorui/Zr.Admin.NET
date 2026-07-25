namespace ZR.Model.System.Tenant
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
        /// 域名绑定：子域名标签（如 acme，对应 acme.shop.com）或租户自定义完整域名（如 acme.com）。
        /// 用于按访问域名自动解析租户，为空表示未绑定（回退 token/主库）。
        /// </summary>
        [SugarColumn(Length = 200)]
        public string Domain { get; set; }

        /// <summary>
        /// 租户名称
        /// </summary>
        [SugarColumn(Length = 100)]
        public string TenantName { get; set; }

        /// <summary>
        /// 企业名称
        /// </summary>
        [SugarColumn(Length = 200)]
        public string CompanyName { get; set; }

        /// <summary>
        /// 联系人
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ContactName { get; set; }

        /// <summary>
        /// 联系电话
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ContactPhone { get; set; }

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
