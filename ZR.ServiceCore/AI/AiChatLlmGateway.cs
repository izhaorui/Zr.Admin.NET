using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Model;
using ZR.ServiceCore.AI.IService;

namespace ZR.ServiceCore.AI
{
    /// <summary>
    /// <see cref="IAiChatLlmGateway"/> 默认实现：原样转发到静态 <c>AiLlmClient</c>，不附加任何额外行为。
    /// 经 [AppService] 注册为 Scoped，供 SysAiChatService 注入；单元测试中以 Mock 替换。
    /// </summary>
    [AppService(ServiceType = typeof(IAiChatLlmGateway), ServiceLifetime = LifeTime.Scoped)]
    public sealed class AiChatLlmGateway : IAiChatLlmGateway
    {
        /// <summary>解析 provider 配置（转发 AiLlmClient）</summary>
        public (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) ResolveProvider(AiOptions options)
            => AiLlmClient.ResolveProvider(options);

        /// <summary>解析视觉 provider 配置（转发 AiLlmClient）</summary>
        public (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) ResolveVisionProvider(AiOptions options)
            => AiLlmClient.ResolveVisionProvider(options);

        /// <summary>多模态看图（通过重试包装层转发）</summary>
        public Task<AiLlmClient.ChatToolResult> ChatWithImagesAsync(AiOptions options, string systemPrompt, string textPrompt, List<string> imageUrls, string scene = null)
            => AiLlmClientRetryWrapper.ChatWithImagesAsyncWithRetry(options, systemPrompt, textPrompt, imageUrls, scene);

        /// <summary>非流式 function calling 单轮调用（通过重试包装层转发）</summary>
        public Task<AiLlmClient.ChatToolResult> ChatWithToolsAsync(AiOptions options, object[] messages, object[] tools, string scene = null)
            => AiLlmClientRetryWrapper.ChatWithToolsAsyncWithRetry(options, messages, tools, scene);

        /// <summary>流式 function calling 单轮调用（转发 wrapper：不做重试，仅把异常标准化为 AiException）</summary>
        public IAsyncEnumerable<AiLlmClient.AiStreamChunk> StreamChatWithToolsAsync(AiOptions options, object[] messages, object[] tools, string scene = null, CancellationToken cancellationToken = default)
            => AiLlmClientRetryWrapper.StreamChatWithToolsAsync(options, messages, tools, scene, cancellationToken);
    }
}
