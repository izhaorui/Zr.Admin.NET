namespace ZR.Model.AI
{
    /// <summary>
    /// 知识库检索审计流水（每次 search_knowledge 工具调用一条）。
    /// <para>
    /// 只记"检索行为"本身，不记片段原文：<see cref="HitTitles"/> 存命中文档名摘要，
    /// 目的是回答"为什么没答上来"（空结果 / 只命中无关文档 / 上游超时），
    /// 不把知识库正文复制进业务库，避免审计表变成第二份文档库。
    /// </para>
    /// <para>
    /// <see cref="Question"/> 落库前截断到 500 字。<see cref="TraceId"/> 与同一请求的
    /// ai_call_log.TraceId 一致，可把"模型调用"与"它发起的检索"串起来排查。
    /// </para>
    /// </summary>
    [SugarTable("ai_knowledge_query_log", "知识库检索审计")]
    [Tenant("0")]
    [SugarIndex("idx_ai_kb_query_time", nameof(CreateTime), OrderByType.Desc)]
    [SugarIndex("idx_ai_kb_query_status", nameof(Status), OrderByType.Asc)]
    [SugarIndex("idx_ai_kb_query_user", nameof(UserId), OrderByType.Asc)]
    public class AiKnowledgeQueryLog : IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long Id { get; set; }

        /// <summary>检索语句（已截断 500 字，保留原文便于复盘"用户到底问了什么"）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string Question { get; set; }

        /// <summary>知识库标识（generic 渠道原样透传，可空）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string KbId { get; set; }

        /// <summary>知识库展示名（出问题时能直接看出是哪个库）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string KbName { get; set; }

        /// <summary>检索渠道：generic / dify / 未来自建</summary>
        [SugarColumn(Length = 32, IsNullable = true)]
        public string Provider { get; set; }

        /// <summary>实际请求的返回条数上限</summary>
        public int TopK { get; set; }

        /// <summary>本次实际命中片段数（0 表示知识库未覆盖）</summary>
        public int HitCount { get; set; }

        /// <summary>最高相关度（0 表示上游未返回分数）</summary>
        [SugarColumn(ColumnDataType = "decimal(6,4)", IsNullable = true)]
        public decimal? TopScore { get; set; }

        /// <summary>检索耗时（毫秒，含上游 HTTP 与解析）</summary>
        public long DurationMs { get; set; }

        /// <summary>success 有命中 / empty 未覆盖 / failed 失败</summary>
        [SugarColumn(Length = 16)]
        public string Status { get; set; }

        /// <summary>错误分类：timeout / unreachable / auth / not_found / rate_limited / server_error / parse / config / invalid_args / exception</summary>
        [SugarColumn(Length = 32, IsNullable = true)]
        public string ErrorCode { get; set; }

        /// <summary>面向排查的错误摘要（不含地址与密钥）</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string ErrorMsg { get; set; }

        /// <summary>命中片段标题摘要（最多 5 条，用 | 分隔），用于判断"命中了但都不相关"</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string HitTitles { get; set; }

        /// <summary>触发用户ID（0 表示无登录上下文）</summary>
        public long UserId { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string UserName { get; set; }

        /// <summary>租户快照；单库部署固定系统库标识</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TenantId { get; set; }

        /// <summary>与 ai_call_log.TraceId 同源，用于串联"模型调用 → 工具检索"</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TraceId { get; set; }

        [SugarColumn(InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }
    }
}