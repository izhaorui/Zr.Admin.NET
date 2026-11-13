namespace ZR.AiKnowledge.Model
{
    /// <summary>知识库检索审计的列表查询条件。</summary>
    public class AiKnowledgeQueryDto
    {
        /// <summary>起始时间（含）</summary>
        public DateTime? BeginTime { get; set; }

        /// <summary>结束时间（含）</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>按问题内容模糊匹配</summary>
        public string Keyword { get; set; }

        /// <summary>success / empty / failed；空=全部</summary>
        public string Status { get; set; }

        /// <summary>检索渠道（generic / dify …）</summary>
        public string Provider { get; set; }

        /// <summary>知识库标识（多库时按库下钻）</summary>
        public string KbId { get; set; }

        /// <summary>按触发用户过滤</summary>
        public long? UserId { get; set; }

        public int PageNum { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }

    /// <summary>一条检索审计（管理端展示）。</summary>
    public class AiKnowledgeQueryLogDto
    {
        public long Id { get; set; }

        public DateTime? CreateTime { get; set; }

        public string Question { get; set; }

        public string Provider { get; set; }

        public string KbName { get; set; }

        public string KbId { get; set; }

        /// <summary>success / empty / failed</summary>
        public string Status { get; set; }

        /// <summary>命中的片段数（0 表示知识库未覆盖）</summary>
        public int HitCount { get; set; }

        /// <summary>本次请求的 topK</summary>
        public int TopK { get; set; }

        /// <summary>最高相关度（上游未返回分数时为 null）</summary>
        public double? TopScore { get; set; }

        /// <summary>检索耗时（毫秒）</summary>
        public long DurationMs { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMsg { get; set; }

        /// <summary>命中文档名摘要，便于判断"命中了但都不相关"</summary>
        public string HitTitles { get; set; }

        public long UserId { get; set; }

        public string UserName { get; set; }

        /// <summary>与 ai_call_log.TraceId 同源</summary>
        public string TraceId { get; set; }
    }

    /// <summary>知识库检索统计（概览）。</summary>
    public class AiKnowledgeQueryStatDto
    {
        /// <summary>统计窗口（天）</summary>
        public int Days { get; set; }

        public long Total { get; set; }

        /// <summary>有命中</summary>
        public long Hit { get; set; }

        /// <summary>检索成功但知识库未覆盖</summary>
        public long Empty { get; set; }

        /// <summary>上游/配置/解析失败</summary>
        public long Failed { get; set; }

        /// <summary>命中率 = Hit / Total（0-1，保留 4 位）</summary>
        public double HitRate { get; set; }

        /// <summary>平均耗时（毫秒）</summary>
        public double AvgDurationMs { get; set; }

        /// <summary>最长耗时（毫秒）</summary>
        public long MaxDurationMs { get; set; }

        /// <summary>按天趋势（补齐无数据的天，便于直接画图）</summary>
        public List<AiKnowledgeDailyStatDto> ByDay { get; set; } = new();

        /// <summary>空结果最多的问题（Top 10），用于发现知识库覆盖缺口</summary>
        public List<AiKnowledgeMissedQuestionDto> TopMissed { get; set; } = new();

        /// <summary>按库拆分：多库时用于发现"某个库整体不命中"或"某个库一直失败"</summary>
        public List<AiKnowledgeBaseStatDto> ByBase { get; set; } = new();
    }

    /// <summary>单库检索概览。</summary>
    public class AiKnowledgeBaseStatDto
    {
        public string KbId { get; set; }

        public string KbName { get; set; }

        public long Total { get; set; }

        public long Hit { get; set; }

        public long Empty { get; set; }

        public long Failed { get; set; }

        /// <summary>命中率 = Hit / Total（0-1）</summary>
        public double HitRate { get; set; }

        /// <summary>平均耗时（毫秒）</summary>
        public double AvgDurationMs { get; set; }
    }

    /// <summary>按天统计。</summary>
    public class AiKnowledgeDailyStatDto
    {
        public string Date { get; set; }

        public long Total { get; set; }

        public long Hit { get; set; }

        public long Empty { get; set; }

        public long Failed { get; set; }
    }

    /// <summary>未命中的高频问题。</summary>
    public class AiKnowledgeMissedQuestionDto
    {
        public string Question { get; set; }

        public long Count { get; set; }
    }

    /// <summary>已注册的知识库（管理端自查 / 前端选择器）。</summary>
    public class AiKnowledgeBaseDto
    {
        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>给模型看的用途说明</summary>
        public string Description { get; set; }

        public string Provider { get; set; }

        public bool IsDefault { get; set; }

        public int TopK { get; set; }

        public double MinScore { get; set; }

        /// <summary>上游库标识</summary>
        public string KbId { get; set; }

        /// <summary>库级权限码；空表示不做库级限制</summary>
        public string Permission { get; set; }

        /// <summary>当前登录用户是否可用（已按权限码判断）</summary>
        public bool CanUse { get; set; }

        /// <summary>基础配置是否完整（缺地址/缺数据集时为 false，便于定位"配了但一直报错"）</summary>
        public bool Configured { get; set; }
    }
}