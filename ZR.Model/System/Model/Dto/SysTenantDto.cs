namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 租户输入对象
    /// </summary>
    public class SysTenantDto
    {
        public long Id { get; set; }
        public string TenantId { get; set; }
        /// <summary>
        /// 域名绑定：子域名标签或完整自定义域名，用于按访问域名解析租户。
        /// </summary>
        public string Domain { get; set; }
        public string TenantName { get; set; }
        public string CompanyName { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
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
        /// <summary>
        /// 域名绑定（子域名标签或完整域名），用于按域名查询租户。
        /// </summary>
        public string Domain { get; set; }
        public int? Status { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// 租户开通请求。
    /// </summary>
        public class TenantProvisionDto
    {
        public string TenantId { get; set; }
        /// <summary>
        /// 域名绑定（子域标签或完整自定义域名）。留空则默认等于 TenantId（即开即用）。
        /// </summary>
        public string Domain { get; set; }
        public string TenantName { get; set; }
        public string CompanyName { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public DateTime? ExpireTime { get; set; }
        public string Remark { get; set; }
        public bool InitializeNow { get; set; } = true;
        public bool SeedFromMain { get; set; } = true;
        public bool EnableAfterInit { get; set; } = true;
    }

    /// <summary>
    /// 租户初始化请求。
    /// </summary>
    public class TenantInitializeDto
    {
        public string TenantId { get; set; }
        public bool CreateDatabaseIfNotExists { get; set; } = true;
        public bool SeedFromMain { get; set; } = true;
        public bool EnsureMainTenantRecord { get; set; } = false;
    }

    /// <summary>
    /// 租户续费请求。
    /// </summary>
    public class TenantRenewDto
    {
        public string TenantId { get; set; }
        public int? ExtendDays { get; set; }
        public DateTime? NewExpireTime { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>
    /// 租户删除请求。
    /// </summary>
    public class TenantDecommissionDto
    {
        public string TenantId { get; set; }
        public bool DeleteRecord { get; set; } = false;
        public string Remark { get; set; }
    }

    /// <summary>
    /// 生命周期步骤。
    /// </summary>
    public class TenantLifecycleStep
    {
        public string Step { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 生命周期执行结果。
    /// </summary>
    public class TenantLifecycleResult
    {
        public string TenantId { get; set; }
        public string Action { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<TenantLifecycleStep> Steps { get; set; } = new();
    }
    
    /// <summary>
    /// 套餐定义输出。
    /// </summary>
    public class TenantPlanDto
    {
        public long Id { get; set; }
        public string PlanCode { get; set; }
        public string PlanName { get; set; }
        public int MaxUsers { get; set; }
        public int Status { get; set; }
        public int IsDefault { get; set; }
        public int Sort { get; set; }
        /// <summary>
        /// 套餐关联菜单数量
        /// </summary>
        public int MenuCount { get; set; }
    }

    /// <summary>
    /// 租户套餐分配请求。
    /// </summary>
    public class TenantPlanAssignDto
    {
        public string TenantId { get; set; }
        public string PlanCode { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? MaxUsersOverride { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>
    /// 租户当前套餐。
    /// </summary>
    public class TenantCurrentPlanDto
    {
        public string TenantId { get; set; }
        public string PlanCode { get; set; }
        public string PlanName { get; set; }
        public int MaxUsers { get; set; }
        public int CurrentUsers { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsExpired { get; set; }
    }

    /// <summary>
    /// 租户套餐用量面板。
    /// </summary>
    public class TenantUsageDashboardDto
    {
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public string CompanyName { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public int TenantStatus { get; set; }
        public DateTime? ExpireTime { get; set; }
        public int? DaysToExpire { get; set; }
        public bool ExpireSoon { get; set; }
        public bool IsExpired { get; set; }

        public string PlanCode { get; set; }
        public string PlanName { get; set; }
        public int MaxUsers { get; set; }
        public int CurrentUsers { get; set; }
        public decimal UserUsageRate { get; set; }
    }

    /// <summary>
    /// 登录页租户选择列表项。
    /// </summary>
    public class TenantLoginInfoDto
    {
        public string TenantId { get; set; }
        public string TenantName { get; set; }
    }

    /// <summary>
    /// 租户到期提醒。
    /// </summary>
    public class TenantExpireReminderDto
    {
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public DateTime? ExpireTime { get; set; }
        public int? DaysToExpire { get; set; }
        public bool IsExpired { get; set; }
        public int TenantStatus { get; set; }

        public string PlanCode { get; set; }
        public string PlanName { get; set; }
        public int MaxUsers { get; set; }
        public int CurrentUsers { get; set; }
        public decimal UserUsageRate { get; set; }
    }
}
