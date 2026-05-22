namespace ZR.Model.AI.Dto
{
    public class AiChatRequestDto
    {
        public string Message { get; set; }
        public string SessionId { get; set; }
        public string Model { get; set; }
        public string Provider { get; set; }
    }

    public class AiChatResponseDto
    {
        public string SessionId { get; set; }
        public string Model { get; set; }
        public string Content { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public class AiChatSessionDto
    {
        public string SessionId { get; set; }
        public string Title { get; set; }
        public string Model { get; set; }
        public string LastMessage { get; set; }
        public DateTime? UpdateTime { get; set; }
    }

    public class AiChatMessageDto
    {
        public string Role { get; set; }
        public string MsgType { get; set; }
        public string Content { get; set; }
        public string DataJson { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? TotalTokens { get; set; }
        public DateTime? CreateTime { get; set; }
    }

    public class AiChatStreamDto
    {
        /// <summary>
        /// type类型 session/image/done/error/chunk
        /// </summary>
        public string Type { get; set; }
        public string SessionId { get; set; }
        public string Content { get; set; }
        public object Data { get; set; }
        /// <summary>
        /// 图片URL列表，type=image时使用
        /// </summary>
        public List<string> Images { get; set; }
    }
}
