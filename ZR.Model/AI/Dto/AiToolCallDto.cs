namespace ZR.Model.AI.Dto
{
    /// <summary>
    /// AI 工具调用审计查询参数（治理页「工具调用」）
    /// </summary>
    public class AiToolCallQueryDto : PagerInfo
    {
        /// <summary>目标用户（管理员不传看全量；普通视角强制当前登录人）</summary>
        public long? UserId { get; set; }

        /// <summary>用户名模糊筛选</summary>
        public string UserName { get; set; }

        public string TenantId { get; set; }

        /// <summary>工具名（精确匹配，与工具目录一致）</summary>
        public string ToolName { get; set; }

        /// <summary>归属提供者 ProviderName</summary>
        public string Provider { get; set; }

        /// <summary>结果状态：success / failed / denied / unknown_tool / exception</summary>
        public string Status { get; set; }

        /// <summary>权限码筛选（用于只看被拦下的受限工具调用）</summary>
        public string Permission { get; set; }

        /// <summary>会话ID（按一轮对话回放工具轨迹）</summary>
        public long? SessionId { get; set; }

        /// <summary>TraceId（与 ai_call_log 串联）</summary>
        public string TraceId { get; set; }

        /// <summary>开始时间（默认最近 7 天）</summary>
        public DateTime? BeginTime { get; set; }

        /// <summary>结束时间（默认当前时间）</summary>
        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// AI 工具调用审计行（治理页明细列表）
    /// </summary>
    public class AiToolCallLogDto
    {
        /// <summary>调用时间</summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>用户（UserId=0 时显示"系统任务"）</summary>
        public string UserName { get; set; }

        public string TenantId { get; set; }

        /// <summary>工具名</summary>
        public string ToolName { get; set; }

        /// <summary>归属提供者 ProviderName</summary>
        public string Provider { get; set; }

        /// <summary>工具定义的权限码（denied 行即缺这个权限）</summary>
        public string Permission { get; set; }

        /// <summary>success / failed / denied / unknown_tool / exception</summary>
        public string Status { get; set; }

        /// <summary>耗时（毫秒）</summary>
        public long DurationMs { get; set; }

        /// <summary>参数摘要（已掩码敏感字段，最长 500 字）</summary>
        public string ArgsSummary { get; set; }

        /// <summary>失败原因摘要</summary>
        public string ErrorMsg { get; set; }

        public string TraceId { get; set; }

        public long SessionId { get; set; }
    }
}