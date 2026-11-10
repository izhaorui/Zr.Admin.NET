namespace ZR.AiKnowledge.Model
{
    /// <summary>知识库连通性测试入参。</summary>
    public class AiKnowledgeTestInput
    {
        /// <summary>检索问题（必填，≤500 字）</summary>
        public string Query { get; set; }

        /// <summary>返回条数（1-20；不传时取配置 TopK）</summary>
        public int? TopK { get; set; }
    }
}
