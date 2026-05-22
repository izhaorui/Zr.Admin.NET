namespace ZR.Model.AI
{
    /// <summary>
    /// AI ÁÄÌìÏûÏ¢
    /// </summary>
    [SugarTable("ai_chat_message")]
    [Tenant(0)]
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
