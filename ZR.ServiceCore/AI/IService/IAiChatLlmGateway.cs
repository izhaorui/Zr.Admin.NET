using Infrastructure.AI;
using Infrastructure.Model;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// AI 大模型调用网关：对静态 <c>AiLlmClient</c> 的薄封装，供对话编排层依赖注入使用。
    /// 存在意义：
    /// ① 编排层（SysAiChatService）不再直接依赖静态类，单元测试可替换为 Mock 验证工具回灌、失败兜底等分支；
    /// ② 后续若需统一加重试/降级/provider 路由，只改实现不影响编排层。
    /// 仅做转发，不改变 AiLlmClient 的既有语义（异常类型、token 统计、流式事件格式均保持不变）。
    /// </summary>
    public interface IAiChatLlmGateway
    {
        /// <summary>
        /// 解析本次调用实际生效的 provider 配置（与 <c>AiLlmClient.ResolveProvider</c> 同语义）
        /// </summary>
        /// <param name="options">AI 配置</param>
        /// <returns>(Provider, BaseUrl, ChatEndpoint, Model, ApiKey)</returns>
        (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) ResolveProvider(AiOptions options);

        /// <summary>解析视觉模型配置（与 <c>AiLlmClient.ResolveVisionProvider</c> 同语义）</summary>
        (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) ResolveVisionProvider(AiOptions options);

        /// <summary>多模态看图（无工具）。imageUrls 为 http(s) 或 data URI。</summary>
        Task<AiLlmClient.ChatToolResult> ChatWithImagesAsync(AiOptions options, string systemPrompt, string textPrompt, List<string> imageUrls, string scene = null);

        /// <summary>
        /// 纯文本一问一答（无工具、无重试，与 <c>AiLlmClient.ChatAsync</c> 同语义）。
        /// 供编排层的内部辅助调用使用（历史摘要等）：这类调用不应绕过网关，否则单元测试无法替换，
        /// 且会把真实 HTTP 带进测试进程。
        /// </summary>
        /// <param name="options">AI 配置</param>
        /// <param name="systemPrompt">system 提示</param>
        /// <param name="userPrompt">user 内容</param>
        /// <param name="scene">调用场景（用于用量统计）</param>
        /// <returns>模型返回的文本（失败时由底层抛 AiException）</returns>
        Task<string> ChatAsync(AiOptions options, string systemPrompt, string userPrompt, string scene = null);

        /// <summary>
        /// 非流式（stream=false）function calling 单轮调用
        /// </summary>
        /// <param name="options">AI 配置</param>
        /// <param name="messages">完整消息数组（system/user/assistant/tool）</param>
        /// <param name="tools">工具定义数组</param>
        /// <param name="scene">调用场景（用于用量统计）</param>
        /// <returns>本轮文本与待执行工具调用</returns>
        Task<AiLlmClient.ChatToolResult> ChatWithToolsAsync(AiOptions options, object[] messages, object[] tools, string scene = null);

        /// <summary>
        /// 流式（SSE）function calling 单轮调用：逐块产出 delta，最后一块为 finish（携带聚合结果）
        /// </summary>
        /// <param name="options">AI 配置</param>
        /// <param name="messages">完整消息数组</param>
        /// <param name="tools">工具定义数组</param>
        /// <param name="scene">调用场景</param>
        /// <param name="cancellationToken">取消令牌（客户端断开/超时）</param>
        /// <returns>事件流</returns>
        IAsyncEnumerable<AiLlmClient.AiStreamChunk> StreamChatWithToolsAsync(AiOptions options, object[] messages, object[] tools, string scene = null, CancellationToken cancellationToken = default);
    }
}
