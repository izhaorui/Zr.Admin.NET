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

        [SugarColumn(InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }
    }
}
