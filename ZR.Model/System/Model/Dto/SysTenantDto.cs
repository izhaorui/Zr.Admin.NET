namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 租户输入对象
    /// </summary>
    public class SysTenantDto
    {
        public long Id { get; set; }
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public int Status { get; set; }
        public DateTime? ExpireTime { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>
    /// 租户查询对象
    /// </summary>
    public class SysTenantQueryDto : PagerInfo
    {
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public int? Status { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}
