using System.Threading;
using ZR.Model.AI.Dto;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// 全局 AI 助手（办工助手）：会话管理 + 工具调用编排。
    /// 数据落 ai_chat_session / ai_chat_message，按 UserId 隔离。
    /// 编排流程：历史上下文 + 系统提示 → LLM(function calling) → 执行工具回灌 → 得到最终回复。
    /// </summary>
    public interface ISysAiChatService
    {
        /// <summary>当前用户会话列表（更新时间倒序，最多 50）</summary>
        Task<List<SysAiChatSessionDto>> ListSessionsAsync(long userId);

        /// <summary>新建空会话</summary>
        Task<SysAiChatSessionDto> CreateSessionAsync(long userId);

        /// <summary>重命名会话</summary>
        Task<int> RenameSessionAsync(long sessionId, long userId, string title);

        /// <summary>删除会话（连同其全部消息）</summary>
        Task<int> DeleteSessionAsync(long sessionId, long userId);

        /// <summary>会话详情：标题 + 消息（升序）</summary>
        Task<SysAiChatDetailDto> GetMessagesAsync(long sessionId, long userId, int limit = 0);

        /// <summary>
        /// 当前用户可见的工具展示名清单（供前端渲染"正在调用 XX"，避免前端硬编码工具中文名）。
        /// 声明了 Permission 的工具仅对有权限用户返回；Label 未登记时前端应降级显示 Name。
        /// </summary>
        Task<List<AiToolCatalogItemDto>> GetMyToolCatalogAsync(long userId);

        /// <summary>
        /// 全量 AI 工具目录（管理端排查用）：内置工具 + 各 Provider 注册的扩展工具，<b>不做权限过滤</b>。
        /// 返回 Name/Label/Provider/Source/Permission/Description，用于定位工具归属与实现模块；
        /// 普通用户的"可见工具清单"仍走 <see cref="GetMyToolCatalogAsync"/>，两者不可混用。
        /// </summary>
        Task<List<AiToolCatalogDetailDto>> GetToolCatalogAsync();

        /// <summary>
        /// 发送一条消息并获取回复。内部可能多次调用 LLM + 执行工具。
        /// SessionId&lt;=0 时自动新建；首轮会依据提问内容自动生成标题。
        /// </summary>
        Task<SysAiChatResultDto> ChatAsync(long sessionId, long userId, string message, IReadOnlyList<string> imageUrls = null);

        /// <summary>
        /// 流式对话（SSE 事件流）：编排语义与 ChatAsync 完全一致，
        /// 仅模型调用走 stream=true。事件协议见 SysAiChatStreamDto：
        /// delta=增量文本、tool=工具执行状态、done=整轮结束(含落库结果)、error=异常终止。
        /// 调用方需逐条序列化输出到 SSE；支持传入取消令牌以在客户端断开时尽快停止流。
        /// </summary>
        IAsyncEnumerable<SysAiChatStreamDto> StreamChatAsync(long sessionId, long userId, string message, IReadOnlyList<string> imageUrls = null, CancellationToken cancellationToken = default);
    }
}
