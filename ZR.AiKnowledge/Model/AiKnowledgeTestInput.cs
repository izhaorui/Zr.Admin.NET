namespace ZR.AiKnowledge.Model
{
    /// <summary>知识库连通性测试入参。</summary>
    public class AiKnowledgeTestInput
    {
        /// <summary>检索问题（必填，≤500 字）</summary>
        public string Query { get; set; }

        /// <summary>返回条数（1-20；不传时取该库配置 TopK）</summary>
        public int? TopK { get; set; }

        /// <summary>要测试的库标识；不传时测默认库</summary>
        public string KbId { get; set; }
    }
}
