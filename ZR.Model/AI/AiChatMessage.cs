namespace ZR.Model.AI
{
    /// <summary>
    /// AI 聊天消息（租户业务数据）。
    /// <para>
    /// 路由：多租户关闭时靠 [Tenant("0")] 固定系统库；多租户开启时因未实现 IMainDbEntity，
    /// 由框架路由到当前租户库，随租户数据自治。故不可加 IMainDbEntity（加了会导致开启时也进系统库）。
    /// </para>
    /// </summary>
    [SugarTable("ai_chat_message")]
    [Tenant("0")]
    // 热查询索引：历史回灌(ResolveHistoryAsync)/GetMessagesAsync/删除会话都按 SessionId+UserId 等值过滤、
    // 再按 CreateTime 排序。等值前缀(SessionId,UserId)+CreateTime 可定位到单会话并免排序，
    // 防止消息表随使用量增长退化为全表扫描；MessageId 是聚集主键(隐含在索引行中)，仅作同刻排序兜底。
    // 注意：SugarIndex 只在建表时生效，存量库需手工补建（幂等 DDL 见交付说明）。
    [SugarIndex("idx_ai_chat_msg_session", nameof(SessionId), OrderByType.Asc)]
    [SugarIndex("idx_ai_chat_msg_session", nameof(UserId), OrderByType.Asc)]
    [SugarIndex("idx_ai_chat_msg_session", nameof(CreateTime), OrderByType.Asc)]
    public class AiChatMessage
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long MessageId { get; set; }

        public long SessionId { get; set; }

        public long UserId { get; set; }

        [SugarColumn(Length = 20)]
        public string Role { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string MsgType { get; set; }

        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string Content { get; set; }

        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string DataJson { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string Model { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? PromptTokens { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? CompletionTokens { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? TotalTokens { get; set; }

        /// <summary>
        /// 回答反馈：1=赞，-1=踩，NULL=未评价。只有 assistant 消息会被写值，用于评估提示词与模型效果。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? Feedback { get; set; }

        /// <summary>点踩原因（用户可填，入库前截断 200 字）；点赞或未填为 NULL。</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string FeedbackNote { get; set; }

        /// <summary>反馈时间；取消评价时清空</summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? FeedbackTime { get; set; }

        [SugarColumn(InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }
    }
}
