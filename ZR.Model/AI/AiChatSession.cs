namespace ZR.Model.AI
{
    /// <summary>
    /// AI 助手会话（租户业务数据）。
    /// <para>
    /// 路由：多租户关闭时靠 [Tenant("0")] 固定系统库；多租户开启时因未实现 IMainDbEntity，
    /// 由框架路由到当前租户库，随租户数据自治。故不可加 IMainDbEntity（加了会导致开启时也进系统库）。
    /// </para>
    /// </summary>
    [SugarTable("ai_chat_session")]
    [Tenant("0")]
    // 会话列表(ListSessionsAsync)按 UserId 过滤、UpdateTime 倒序取最近 50 条，
    // (UserId,UpdateTime DESC) 可免排序直接取 TopN；其余会话操作均按 SessionId(主键)定位，无需额外索引。
    // 注意：SugarIndex 只在建表时生效，存量库需手工补建（幂等 DDL 见交付说明）。
    [SugarIndex("idx_ai_chat_session_user", nameof(UserId), OrderByType.Asc)]
    [SugarIndex("idx_ai_chat_session_user", nameof(UpdateTime), OrderByType.Desc)]
    public class AiChatSession
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long SessionId { get; set; }

        public long UserId { get; set; }

        [SugarColumn(Length = 100)]
        public string Title { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string Model { get; set; }

        [SugarColumn(InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }

        [SugarColumn(IsNullable = true)]
        public DateTime? UpdateTime { get; set; }
    }
}
