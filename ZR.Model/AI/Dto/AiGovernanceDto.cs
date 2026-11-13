namespace ZR.Model.AI.Dto
{
    public class AiPolicyQueryDto : PagerInfo
    {
        public string TenantId { get; set; }
        public string ScopeType { get; set; }
        public long? SubjectId { get; set; }
        public string Scene { get; set; }
        public int? Status { get; set; }
    }

    public class AiPolicySaveDto
    {
        public long Id { get; set; }
        public string ScopeType { get; set; }
        public string TenantId { get; set; }
        public long SubjectId { get; set; }
        public string Scene { get; set; } = "*";
        public int? IsEnabled { get; set; }
        public long? DailyTokenLimit { get; set; }
        public long? MonthlyTokenLimit { get; set; }
        public decimal? DailyAmountLimit { get; set; }
        public decimal? MonthlyAmountLimit { get; set; }
        public int? ConcurrentLimit { get; set; }
        public int Status { get; set; }
        public string Remark { get; set; }
    }

    public class AiPolicyListDto
    {
        public long Id { get; set; }
        public string ScopeType { get; set; }
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public long SubjectId { get; set; }
        public string SubjectName { get; set; }
        public string Scene { get; set; }
        public int? IsEnabled { get; set; }
        public long? DailyTokenLimit { get; set; }
        public long? MonthlyTokenLimit { get; set; }
        public decimal? DailyAmountLimit { get; set; }
        public decimal? MonthlyAmountLimit { get; set; }
        public int? ConcurrentLimit { get; set; }
        public int Status { get; set; }
        public string Remark { get; set; }
    }

    public class AiPolicySubjectDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Extra { get; set; }
    }

    public class AiModelPriceQueryDto : PagerInfo
    {
        public string Provider { get; set; }
        public string Model { get; set; }
        public int? Status { get; set; }
    }

    public class AiModelPriceSaveDto
    {
        public long Id { get; set; }
        public string Provider { get; set; }
        public string Model { get; set; }
        public decimal InputPricePerMillion { get; set; }
        public decimal OutputPricePerMillion { get; set; }
        public string Currency { get; set; } = "CNY";
        public int Status { get; set; }
        public string Remark { get; set; }
    }

    public class AiConfigCheckDto
    {
        public bool Valid { get; set; }
        public bool Enabled { get; set; }
        public string Provider { get; set; }
        public string BaseUrl { get; set; }
        public string Endpoint { get; set; }
        public string Model { get; set; }
        public bool ApiKeyConfigured { get; set; }
        public bool VisionConfigured { get; set; }
        public string VisionModel { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// AI 配置的“实际生效来源”诊断：明确当前读的是 ai_config 还是 ai.json、
    /// 渠道来自 ai_provider 还是文件，并给出合并优先级与告警（密钥一律掩码）。
    /// </summary>
    public class AiConfigDiagnosticsDto
    {
        /// <summary>配置源策略：auto / db / file。</summary>
        public string Source { get; set; }

        /// <summary>当前宿主环境名（auto 仅 Production 读库）。</summary>
        public string Environment { get; set; }

        /// <summary>本次是否读库。</summary>
        public bool DatabaseEnabled { get; set; }

        /// <summary>全局参数权威来源：ai_config / ai.json。</summary>
        public string GlobalSource { get; set; }

        /// <summary>渠道权威来源：ai_provider / ai.json。</summary>
        public string ProviderSource { get; set; }

        /// <summary>生效渠道数量。</summary>
        public int ProviderCount { get; set; }

        /// <summary>生效渠道展示名（如“主渠道”）。</summary>
        public string ActiveProviderName { get; set; }

        public bool Enable { get; set; }
        public string Provider { get; set; }

        /// <summary>BaseUrl（已掩码路径参数）。</summary>
        public string BaseUrl { get; set; }

        public string Endpoint { get; set; }
        public string Model { get; set; }

        /// <summary>ApiKey 掩码值，仅用于确认“是否配置”，不可还原。</summary>
        public string ApiKeyMasked { get; set; }

        public bool ApiKeyConfigured { get; set; }
        public string VisionProvider { get; set; }
        public string VisionModel { get; set; }
        public int TimeoutSeconds { get; set; }

        /// <summary>合并优先级说明（从高到低），排查时直接看这里。</summary>
        public List<string> Precedence { get; set; } = new();

        /// <summary>回退 / 覆盖 / 环境不一致等需要关注的提示。</summary>
        public List<string> Warnings { get; set; } = new();
    }

    public class AiGovernanceCapabilitiesDto
    {
        public string TenantId { get; set; }
        public bool IsPlatformAdmin { get; set; }
        public bool CanManageGlobalPolicy { get; set; }
        public bool CanManageModelPrice { get; set; }
        public bool CanCheckProvider { get; set; }
    }

    public class AiHealthCheckDto
    {
        public bool Healthy { get; set; }
        public string Provider { get; set; }
        public string Model { get; set; }
        public long LatencyMs { get; set; }
        public int? HttpStatusCode { get; set; }
        public string ProviderRequestId { get; set; }
        public string Message { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public class AiModelCatalogDto
    {
        public List<AiProviderOptionDto> Providers { get; set; } = new();
    }

    public class AiProviderOptionDto
    {
        public string Provider { get; set; }
        public string Label { get; set; }
        public List<string> Models { get; set; } = new();
    }
}
