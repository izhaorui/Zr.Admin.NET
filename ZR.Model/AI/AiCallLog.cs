namespace ZR.Model.AI
{
    /// <summary>
    /// AI 调用 token 用量审计流水（每次模型 HTTP 调用一条，供审计/统计）。
    /// <para>
    /// 幂等约束：RequestId 上建有唯一索引 uk_ai_call_request，保证"同一调用租约"在并发/重入结算时只落一行。
    /// 粒度是"每次 HTTP 尝试一条流水"：重试/换渠道在 Infrastructure.AI.AiLlmClientRetryWrapper（AiLlmClient 外层），
    /// 每次尝试都重新 BeginAsync 生成新 RequestId、各自归属实际渠道与单价，唯一索引不去重重试（设计而非缺陷）。
    /// 失败尝试（超时/取消等）无 usage 时仅按输入估值记账，避免按满额输出高估、也避免重试重复扣满额。
    /// </para>
    /// </summary>
    [SugarTable("ai_call_log")]
    [Tenant(0)]
    [SugarIndex("uk_ai_call_request", nameof(RequestId), OrderByType.Asc, true)]
    [SugarIndex("idx_ai_call_quota", nameof(TenantId), OrderByType.Asc)]
    [SugarIndex("idx_ai_call_quota", nameof(UserId), OrderByType.Asc)]
    [SugarIndex("idx_ai_call_quota", nameof(Scene), OrderByType.Asc)]
    [SugarIndex("idx_ai_call_quota", nameof(CreateTime), OrderByType.Asc)]
    [SugarIndex("idx_ai_call_time", nameof(TenantId), OrderByType.Asc)]
    [SugarIndex("idx_ai_call_time", nameof(CreateTime), OrderByType.Desc)]
    public class AiCallLog : IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long Id { get; set; }

        /// <summary>能力标识：ai_chat / wf_generate / lang_translate / login_security 等</summary>
        [SugarColumn(Length = 64)]
        public string Scene { get; set; }

        [SugarColumn(Length = 32)]
        public string Provider { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string Model { get; set; }

        /// <summary>租户快照；平台或无法确定租户时为 MainDb。</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TenantId { get; set; }

        /// <summary>
        /// 一次模型 HTTP 调用的唯一标识（结算幂等键，见类注释）。
        /// 由治理层 BeginAsync 生成，列上建唯一索引 uk_ai_call_request：同一租约重复结算只落一行。
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string RequestId { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string TraceId { get; set; }

        /// <summary>1 成功、0 失败；历史记录为空，统计成功率时排除。</summary>
        [SugarColumn(IsNullable = true)]
        public int? Success { get; set; }

        /// <summary>success/rejected/timeout/cancelled/http/provider/parse/empty/unknown。</summary>
        [SugarColumn(Length = 32, IsNullable = true)]
        public string Status { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string ErrorType { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? HttpStatusCode { get; set; }

        public long DurationMs { get; set; }

        [SugarColumn(Length = 128, IsNullable = true)]
        public string ProviderRequestId { get; set; }

        [SugarColumn(DefaultValue = "0")]
        public int IsStream { get; set; }

        /// <summary>输入 token（prompt）</summary>
        public int PromptTokens { get; set; }

        /// <summary>输出 token（completion）</summary>
        public int CompletionTokens { get; set; }

        /// <summary>合计 token</summary>
        public int TotalTokens { get; set; }

        /// <summary>触发用户ID（请求登录人；后台任务无登录上下文时为 0）</summary>
        public long UserId { get; set; }

        /// <summary>触发用户（请求登录人；后台任务无登录上下文时为空）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string UserName { get; set; }

        [SugarColumn(Length = 1000, IsNullable = true)]
        public string ErrorMsg { get; set; }

        [SugarColumn(ColumnDataType = "decimal(18,6)", DefaultValue = "0")]
        public decimal InputAmount { get; set; }

        [SugarColumn(ColumnDataType = "decimal(18,6)", DefaultValue = "0")]
        public decimal OutputAmount { get; set; }

        [SugarColumn(ColumnDataType = "decimal(18,6)", DefaultValue = "0")]
        public decimal EstimatedAmount { get; set; }

        [SugarColumn(Length = 8, DefaultValue = "CNY")]
        public string Currency { get; set; } = "CNY";

        /// <summary>是否取得 Provider 返回的实际 usage。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int HasUsage { get; set; }

        [SugarColumn(InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }
    }
}
