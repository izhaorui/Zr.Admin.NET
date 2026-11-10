using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.AI.Knowledge
{
    /// <summary>
    /// 知识库检索统一契约：本框架只做「检索 + 引用」，不负责文档切片与向量化。
    /// <para>
    /// 实现可以是外部服务（Dify / 百炼 / 自建检索接口），也可以是后续自建的本地索引；
    /// AI 助手的 <c>search_knowledge</c> 工具只依赖本接口，换后端不影响提示词、工具契约与前端。
    /// </para>
    /// </summary>
    public interface IKnowledgeProvider
    {
        /// <summary>渠道名（配置 AiKnowledge:Provider 的值），如 generic / dify。</summary>
        string Name { get; }

        /// <summary>按自然语言问题检索片段。可预期失败请抛 <see cref="KnowledgeProviderException"/>。</summary>
        Task<KnowledgeSearchResult> SearchAsync(KnowledgeQuery query, CancellationToken cancellationToken = default);
    }

    /// <summary>一次知识库检索请求。TopK/MinScore 已由调用方按配置归一化。</summary>
    public sealed class KnowledgeQuery
    {
        /// <summary>用户问题原文（已去除空白）</summary>
        public string Question { get; set; }

        /// <summary>返回条数上限（1-20）</summary>
        public int TopK { get; set; } = 5;

        /// <summary>相似度下限（0 表示不启用）</summary>
        public double MinScore { get; set; }

        /// <summary>知识库标识（部分实现需要，如自建服务按库路由）</summary>
        public string KbId { get; set; }

        /// <summary>附加过滤条件（元数据/标签，原样透传，由实现决定语义）</summary>
        public IReadOnlyDictionary<string, string> Filters { get; set; }
    }

    /// <summary>检索到的一个片段。Content 为原文，其余字段用于「引用溯源」展示。</summary>
    public sealed class KnowledgeHit
    {
        /// <summary>片段原文</summary>
        public string Content { get; set; }

        /// <summary>相关度（0-1，缺失时填 0）</summary>
        public double Score { get; set; }

        /// <summary>文档名/标题</summary>
        public string Title { get; set; }

        /// <summary>出处：文档链接、文件路径或知识库条目地址</summary>
        public string Source { get; set; }

        /// <summary>定位信息：页码、章节、段落序号等</summary>
        public string Locator { get; set; }
    }

    /// <summary>检索结果集合。Hits 为空表示「知识库未覆盖」，由调用方决定如何提示模型。</summary>
    public sealed class KnowledgeSearchResult
    {
        /// <summary>知识库展示名（用于回灌文本的「来源」）</summary>
        public string KbName { get; set; }

        /// <summary>片段列表（已按相关度降序）</summary>
        public List<KnowledgeHit> Hits { get; set; } = new();
    }

    /// <summary>
    /// 知识库可预期异常：<see cref="UserMessage"/> 可安全回灌给模型/用户，
    /// 内部细节（地址、密钥、上游报文）只进日志，不上抛。
    /// </summary>
    public class KnowledgeProviderException : Exception
    {
        public KnowledgeProviderException(string message, string userMessage) : base(message)
        {
            UserMessage = userMessage;
        }

        /// <summary>面向用户/模型的友好提示（不含地址与密钥）</summary>
        public string UserMessage { get; }
    }
}
