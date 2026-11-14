using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using NLog;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.Model.System.Dto;
using ZR.ServiceCore.AI.Charts;
using ZR.ServiceCore.AI.IService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.AI
{
    /// <summary>
    /// 全局 AI 助手（办工助手）：
    /// 会话管理（ai_chat_session / ai_chat_message，按 UserId 隔离）+
    /// 对话编排：历史上下文 + 系统提示 → LLM(function calling) → 执行工具回灌 → 最终回复。
    /// 内置办工工具（日程/周报），并聚合各模块 IAiAssistantToolProvider 注册的扩展工具（如工作流待办）。
    /// </summary>
    [AppService(ServiceType = typeof(ISysAiChatService), ServiceLifetime = LifeTime.Transient)]
    public class SysAiChatService : BaseService<AiChatSession>, ISysAiChatService
    {
        private readonly IAiChatLlmGateway _llm;
        private readonly ISysPermissionService _permissionService;
        private readonly IReadOnlyList<IAiAssistantToolProvider> _toolProviders;
        private readonly IReadOnlyList<AiToolDef> _providerToolDefs;
        private readonly Dictionary<string, IAiAssistantToolProvider> _toolProviderMap;
        private readonly Dictionary<string, AiToolDef> _toolDefMap;
        private readonly IAiToolCallLogService _toolCallLogService;
        private readonly IWebHostEnvironment _env;

        /// <summary>执行期工具鉴权用的权限快照（本实例 = 单次请求，按需加载一次）</summary>
        private List<string> _callPerms;
        private bool _callPermsLoaded;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>最大工具轮询次数（防止模型反复要求调用工具造成死循环）</summary>
        private const int MaxToolRounds = 5;
        private const string ToolRetryReminder =
            "上一轮工具全部失败。请根据错误与工具 schema 修正参数后再次调用；若确实无法调用，再向用户简要说明原因，不要复述内部错误原文。";
        /// <summary>回灌历史消息最近窗口条数（窗口内保留原文，窗口之前的内容由增量摘要压缩）</summary>
        private const int MaxHistoryMessages = 20;
        /// <summary>
        /// 单轮摘要最多纳入的旧消息条数，也是历史读取上限。正常增量时水位线之后滑出窗口的只有几条；
        /// 水位线丢失（缓存过期/清理）时按此步长逐轮追赶，既不一次塞爆模型也不丢历史。
        /// </summary>
        private const int SummaryChunkMessages = 100;
        /// <summary>历史摘要缓存分钟数：续聊同一会话（含隔天）时复用增量摘要，避免水位线过期后退化为全量重算</summary>
        private const int SummaryCacheMinutes = 1440;
        /// <summary>单条工具结果回灌给模型的最大长度</summary>
        private const int ToolResultMaxLen = 8000;
        /// <summary>单条用户消息最大长度（与请求 DTO 校验一致，服务端兜底防超长文本烧 token）</summary>
        private const int MaxUserMessageLength = 2000;

        /// <param name="llm">大模型调用网关（封装静态 AiLlmClient，便于测试替换）</param>
        /// <param name="toolProviders">各模块注册的工具提供者（内置办工工具也已实现该接口，见 AiOfficeToolProvider）</param>
        /// <param name="permissionService">权限服务（工具清单按用户权限过滤）</param>
        /// <param name="toolCallLogService">工具调用审计（每次工具执行落一条，用于排查越权/误调用）</param>
        /// <param name="env">宿主环境（本机上传图片转 data URI 时定位 wwwroot）</param>
        public SysAiChatService(IAiChatLlmGateway llm, IEnumerable<IAiAssistantToolProvider> toolProviders,
            ISysPermissionService permissionService, IAiToolCallLogService toolCallLogService, IWebHostEnvironment env)
        {
            _llm = llm;
            _permissionService = permissionService;
            _toolCallLogService = toolCallLogService;
            _env = env;
            _toolProviders = (toolProviders ?? []).ToList();

            var defs = new List<AiToolDef>();
            _toolProviderMap = new Dictionary<string, IAiAssistantToolProvider>(StringComparer.OrdinalIgnoreCase);
            _toolDefMap = new Dictionary<string, AiToolDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in _toolProviders)
            {
                var providerDefs = provider.GetToolDefs();
                if (providerDefs == null || providerDefs.Count == 0) continue;
                foreach (var def in providerDefs)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.Name)) continue;
                    // 工具名全局唯一（CONTRIBUTING 约定）。重复注册若静默保留，同一工具会在前端清单
                    // 与模型请求里各出现两次，且模型调用时归属不确定，因此丢弃并告警。
                    if (_toolProviderMap.TryGetValue(def.Name, out var owner))
                    {
                        _logger.Warn($"AI 工具名重复已忽略：{def.Name}（已由 {owner.ProviderName} 注册，跳过 {provider.ProviderName} 的重复定义）");
                        continue;
                    }
                    defs.Add(def);
                    _toolProviderMap.Add(def.Name, provider);
                    _toolDefMap.Add(def.Name, def);
                }
            }
            _providerToolDefs = defs;
        }

        #region 会话 CRUD

        /// <summary>
        /// 列出用户的会话列表
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>会话列表</returns>
        public async Task<List<SysAiChatSessionDto>> ListSessionsAsync(long userId)
        {
            var list = await Queryable()
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.UpdateTime, OrderByType.Desc)
                .Take(50)
                .ToListAsync();
            return [.. list.Select(m => new SysAiChatSessionDto
            {
                SessionId = m.SessionId,
                Title = m.Title,
                Model = m.Model,
                CreateTime = m.CreateTime,
                UpdateTime = m.UpdateTime
            })];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<SysAiChatSessionDto> CreateSessionAsync(long userId)
        {
            var session = new AiChatSession { UserId = userId, Title = "新对话" };
            session.SessionId = await Context.Insertable(session).ExecuteReturnSnowflakeIdAsync();
            return new SysAiChatSessionDto
            {
                SessionId = session.SessionId,
                Title = session.Title,
                CreateTime = session.CreateTime
            };
        }

        /// <summary>
        /// 重命名会话
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="title">新的会话标题</param>
        /// <returns>受影响的行数</returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> RenameSessionAsync(long sessionId, long userId, string title)
        {
            var rows = await UpdateAsync(
                s => s.SessionId == sessionId && s.UserId == userId,
                s => new AiChatSession { Title = title, UpdateTime = DateTime.Now });
            if (rows <= 0)
            {
                throw new Exception("会话不存在或无权操作");
            }
            return rows;
        }

        /// <summary>
        /// 删除会话及其消息
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>受影响的行数</returns>
        public async Task<int> DeleteSessionAsync(long sessionId, long userId)
        {
            // 先删消息，再删会话；仅允许操作本人会话
            await Context.Deleteable<AiChatMessage>()
                .Where(m => m.SessionId == sessionId && m.UserId == userId)
                .ExecuteCommandAsync();
            var rows = await Context.Deleteable<AiChatSession>()
                .Where(m => m.SessionId == sessionId && m.UserId == userId)
                .ExecuteCommandAsync();
            if (rows <= 0)
            {
                throw new Exception("会话不存在或无权操作");
            }
            return rows;
        }

        /// <summary>
        /// 获取会话的消息列表
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>会话详情</returns>
        /// <exception cref="Exception"></exception>
        /// <summary>
        /// 会话历史消息（升序）。limit&gt;0 时只返回最近 limit 条（仍为升序），并用 HasMore 标识更早消息；limit≤0 返回全部。
        /// </summary>
        public async Task<SysAiChatDetailDto> GetMessagesAsync(long sessionId, long userId, int limit = 0)
        {
            var session = await Queryable()
                .Where(m => m.SessionId == sessionId && m.UserId == userId)
                .FirstAsync();
            if (session == null)
            {
                throw new Exception("会话不存在或无权查看");
            }
            List<AiChatMessage> msgs;
            var hasMore = false;
            if (limit > 0)
            {
                var latest = await Context.Queryable<AiChatMessage>()
                    .Where(m => m.SessionId == sessionId && m.UserId == userId)
                    .OrderBy(m => m.CreateTime, OrderByType.Desc)
                    .OrderBy(m => m.MessageId, OrderByType.Desc)
                    .Take(limit + 1)
                    .ToListAsync();
                hasMore = latest.Count > limit;
                if (hasMore)
                {
                    latest.RemoveRange(limit, latest.Count - limit);
                }
                latest.Reverse();
                msgs = latest;
            }
            else
            {
                msgs = await Context.Queryable<AiChatMessage>()
                    .Where(m => m.SessionId == sessionId && m.UserId == userId)
                    .OrderBy(m => m.CreateTime, OrderByType.Asc)
                    .OrderBy(m => m.MessageId, OrderByType.Asc)
                    .ToListAsync();
            }
            var list = msgs.Select(m => new SysAiChatMessageDto
            {
                MessageId = m.MessageId,
                Role = m.Role,
                Content = m.Content,
                CreateTime = m.CreateTime,
                PromptTokens = m.PromptTokens,
                CompletionTokens = m.CompletionTokens,
                TotalTokens = m.TotalTokens,
                Charts = m.Role == "assistant" ? AiChartAssembler.FromDataJson(m.DataJson) : null,
                ImageUrls = m.Role == "user" ? AiChatImageHelper.FromImagesDataJson(m.DataJson) : null,
                Tools = m.Role == "assistant" ? AiChartAssembler.FromToolsDataJson(m.DataJson) : null
            }).ToList();

            return new SysAiChatDetailDto
            {
                SessionId = session.SessionId,
                Title = session.Title,
                Model = session.Model,
                Messages = list,
                HasMore = hasMore
            };
        }

        #endregion 会话 CRUD

        #region 对话编排

        /// <summary>
        /// 进行一次聊天交互
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="userId">用户ID</param>
        /// <param name="message">消息内容</param>
        /// <param name="imageUrls">本轮图片 URL；非空时走视觉模型且不调工具</param>
        /// <returns>聊天结果</returns>
        /// <exception cref="Exception"></exception>
        public async Task<SysAiChatResultDto> ChatAsync(long sessionId, long userId, string message, IReadOnlyList<string> imageUrls = null)
        {
            var context = await PrepareChatContextAsync(sessionId, userId, message, imageUrls);

            if (context.UseVision)
            {
                AiLlmClient.ChatToolResult visionTurn;
                try
                {
                    visionTurn = await _llm.ChatWithImagesAsync(context.Options, context.VisionSystemPrompt, context.VisionUserPrompt,
                        context.ModelImageUrls, AiSceneCatalog.AiChat);
                }
                catch (Exception ex)
                {
                    _logger.Error($"AI 视觉调用异常 sessionId={context.Session.SessionId} userId={userId} model={context.Model} msg={AiHelper.ClipText(context.Message, 200)} err={ex}");
                    throw;
                }
                var visionReply = visionTurn?.Content;
                if (string.IsNullOrWhiteSpace(visionReply))
                {
                    visionReply = "抱歉，我这边没有正常生成回答，请重新描述一下你的问题。";
                }
                var visionUsage = visionTurn != null && (visionTurn.PromptTokens > 0 || visionTurn.CompletionTokens > 0 || visionTurn.TotalTokens > 0);
                return await FinishTurnAsync(context.Session, context.IsNewSession, userId, context.Message, visionReply, context.Model,
                    visionUsage, visionTurn?.PromptTokens ?? 0, visionTurn?.CompletionTokens ?? 0, visionTurn?.TotalTokens ?? 0, null, context.StoreImageUrls);
            }

            // 3. 工具调用编排
            var st = new ChatRoundState();
            string reply = "";
            for (var round = 0; round < MaxToolRounds; round++)
            {
                AiLlmClient.ChatToolResult turn;
                try
                {
                    turn = await _llm.ChatWithToolsAsync(context.Options, context.Messages.ToArray(), context.Tools, AiSceneCatalog.AiChat);
                }
                catch (Exception ex)
                {
                    // 详情只写后端日志；异常上抛，由上层返回友好提示，不让内部细节透出到前端
                    _logger.Error($"AI 模型调用异常 sessionId={context.Session.SessionId} userId={userId} model={context.Model} msg={AiHelper.ClipText(context.Message, 200)} err={ex}");
                    throw;
                }

                RecordRoundUsage(st, turn, round);

                var hasToolCalls = turn.ToolCalls != null && turn.ToolCalls.Count > 0;
                if (!hasToolCalls)
                {
                    if (TryConsumeForceRetry(st, round, context.Messages))
                    {
                        continue;
                    }
                    reply = turn.Content ?? "";
                    break;
                }

                await ExecuteRoundToolsAsync(st, turn, context.Messages, userId, context.Session.SessionId);
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                _logger.Error($"AI 回复为空(触发兜底文案) userId={userId} sessionId={context.Session.SessionId} model={context.Model} " +
                    $"msg={AiHelper.ClipText(context.Message, 200)} history={context.HistoryCount} rounds={string.Join(" | ", st.RoundDiag)}");
                reply = "抱歉，我这边没有正常生成回答，请重新描述一下你的问题。";
            }

            var assembled = AiChartAssembler.Assemble(reply, st.ChartQueries);
            reply = assembled.Reply;
            var charts = assembled.Charts;

            // 4. 落库 + 会话元信息维护
            return await FinishTurnAsync(context.Session, context.IsNewSession, userId, context.Message, reply, context.Model,
                st.HasUsage, st.TotalPromptTokens, st.TotalCompletionTokens, st.TotalTokens, charts, tools: st.ToolTrace);
        }

        /// <summary>
        /// 流式对话（SSE 事件流）：与 ChatAsync 编排完全一致（同一套系统提示/历史/工具/落库/图表），
        /// 仅模型调用走 stream=true。事件类型见 SysAiChatStreamDto：
        /// delta=模型增量文本（实时推送）；tool=工具执行开始/结束；done=整轮结束（含落库结果）。
        /// 模型/业务异常直接向上抛出，由调用方转为 error 事件；成功流必有最后一个 done 事件。
        /// 有图时走视觉模型（无工具，整段回复一次推送，心跳保活等待）。
        /// </summary>
        public async IAsyncEnumerable<SysAiChatStreamDto> StreamChatAsync(long sessionId, long userId, string message, IReadOnlyList<string> imageUrls = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = await PrepareChatContextAsync(sessionId, userId, message, imageUrls);

            if (context.UseVision)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AiLlmClient.ChatToolResult visionTurn;
                try
                {
                    visionTurn = await _llm.ChatWithImagesAsync(context.Options, context.VisionSystemPrompt, context.VisionUserPrompt,
                        context.ModelImageUrls, AiSceneCatalog.AiChat);
                }
                catch (Exception ex)
                {
                    _logger.Error($"AI 视觉调用异常 sessionId={context.Session.SessionId} userId={userId} model={context.Model} msg={AiHelper.ClipText(context.Message, 200)} err={ex}");
                    throw;
                }
                var visionReply = visionTurn?.Content;
                if (string.IsNullOrWhiteSpace(visionReply))
                {
                    _logger.Error($"AI 回复为空(触发兜底文案) userId={userId} sessionId={context.Session.SessionId} model={context.Model} vision=1 msg={AiHelper.ClipText(message, 200)}");
                    visionReply = "抱歉，我这边没有正常生成回答，请重新描述一下你的问题。";
                }
                yield return new SysAiChatStreamDto { Type = "delta", Content = visionReply };
                var visionUsage = visionTurn != null && (visionTurn.PromptTokens > 0 || visionTurn.CompletionTokens > 0 || visionTurn.TotalTokens > 0);
                var visionResult = await FinishTurnAsync(context.Session, context.IsNewSession, userId, context.Message, visionReply, context.Model,
                    visionUsage, visionTurn?.PromptTokens ?? 0, visionTurn?.CompletionTokens ?? 0, visionTurn?.TotalTokens ?? 0, null, context.StoreImageUrls);
                yield return ToDoneEvent(visionResult);
                yield break;
            }

            // 3. 工具调用编排（流式）
            var st = new ChatRoundState();
            string reply = "";
            for (var round = 0; round < MaxToolRounds; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AiLlmClient.ChatToolResult turn = null;
                var bufferDeltas = st.LastRoundAllFailed;
                var buffered = bufferDeltas ? new List<string>() : null;
                // 模型流式调用：逐块转发增量文本，结束时聚合出本轮完整结果。
                // 注意：迭代段内不得被 try/catch 包裹（含 yield return），异常向上冒出由调用方转 error 事件。
                await foreach (var chunk in _llm.StreamChatWithToolsAsync(context.Options, context.Messages.ToArray(), context.Tools, AiSceneCatalog.AiChat, cancellationToken).WithCancellation(cancellationToken))
                {
                    if (chunk.Type == "delta" && !string.IsNullOrEmpty(chunk.Text))
                    {
                        if (bufferDeltas)
                        {
                            buffered.Add(chunk.Text);
                        }
                        else
                        {
                            yield return new SysAiChatStreamDto { Type = "delta", Content = chunk.Text };
                        }
                    }
                    else if (chunk.Type == "tool" && !string.IsNullOrWhiteSpace(chunk.Text))
                    {
                        yield return new SysAiChatStreamDto { Type = "tool", ToolName = chunk.Text, ToolStatus = "start" };
                    }
                    else if (chunk.Type == "finish")
                    {
                        turn = chunk.Result;
                    }
                }
                if (turn == null)
                {
                    throw new HttpRequestException($"AI 服务未返回有效响应（model={context.Model}），请重试");
                }

                RecordRoundUsage(st, turn, round);

                var hasToolCalls = turn.ToolCalls != null && turn.ToolCalls.Count > 0;
                if (!hasToolCalls)
                {
                    if (TryConsumeForceRetry(st, round, context.Messages))
                    {
                        continue;
                    }
                    if (buffered != null)
                    {
                        foreach (var piece in buffered)
                        {
                            yield return new SysAiChatStreamDto { Type = "delta", Content = piece };
                        }
                    }
                    reply = turn.Content ?? "";
                    break;
                }

                if (buffered != null)
                {
                    foreach (var piece in buffered)
                    {
                        yield return new SysAiChatStreamDto { Type = "delta", Content = piece };
                    }
                }

                // 先统一下发 start 事件，再并行执行，最后统一 done：事件顺序与前端契约不变，且降首字延迟
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var call in turn.ToolCalls)
                {
                    yield return new SysAiChatStreamDto { Type = "tool", ToolName = call.Name, ToolStatus = "start" };
                }
                var streamExecs = await ExecuteRoundToolsAsync(st, turn, context.Messages, userId, context.Session.SessionId);
                for (var i = 0; i < turn.ToolCalls.Count; i++)
                {
                    var call = turn.ToolCalls[i];
                    yield return new SysAiChatStreamDto { Type = "tool", ToolName = call.Name, ToolStatus = "done", ToolOk = streamExecs[i].Ok };
                }
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                _logger.Error($"AI 回复为空(触发兜底文案) userId={userId} sessionId={context.Session.SessionId} model={context.Model} " +
                    $"msg={AiHelper.ClipText(message, 200)} history={context.HistoryCount} rounds={string.Join(" | ", st.RoundDiag)}");
                reply = "抱歉，我这边没有正常生成回答，请重新描述一下你的问题。";
            }

            var assembledStream = AiChartAssembler.Assemble(reply, st.ChartQueries);
            reply = assembledStream.Reply;
            var chartsStream = assembledStream.Charts;

            // 4. 落库 + 会话元信息维护
            var result = await FinishTurnAsync(context.Session, context.IsNewSession, userId, context.Message, reply, context.Model,
                st.HasUsage, st.TotalPromptTokens, st.TotalCompletionTokens, st.TotalTokens, chartsStream, tools: st.ToolTrace);
            yield return ToDoneEvent(result);
        }

        private static SysAiChatStreamDto ToDoneEvent(SysAiChatResultDto result)
        {
            return new SysAiChatStreamDto
            {
                Type = "done",
                SessionId = result.SessionId,
                Title = result.Title,
                Model = result.Model,
                Reply = result.Reply,
                IsNewSession = result.IsNewSession,
                PromptTokens = result.PromptTokens,
                CompletionTokens = result.CompletionTokens,
                TotalTokens = result.TotalTokens,
                Charts = result.Charts,
                Tools = result.Tools
            };
        }

        private async Task SaveMessageAsync(long sessionId, long userId, string role, string content, string model,
            int promptTokens = 0, int completionTokens = 0, int totalTokens = 0, string dataJson = null)
        {
            var msg = new AiChatMessage
            {
                SessionId = sessionId,
                UserId = userId,
                Role = role,
                MsgType = string.IsNullOrWhiteSpace(dataJson) ? "text" : (role == "user" ? "image" : "chart"),
                Content = content ?? "",
                DataJson = dataJson,
                Model = model
            };
            // 由 assistant 消息承载本次对话累计 token（一次对话可能多次调用模型），usage 缺失时保持 NULL
            if (promptTokens > 0 || completionTokens > 0 || totalTokens > 0)
            {
                msg.PromptTokens = promptTokens;
                msg.CompletionTokens = completionTokens;
                msg.TotalTokens = totalTokens;
            }
            msg.MessageId = await Context.Insertable(msg).ExecuteReturnSnowflakeIdAsync();
        }

        /// <summary>
        /// 落库本轮用户/助手消息并维护会话元信息（标题/更新时间），返回聊天结果。
        /// token 为 0（模型未返回 usage）时保持 NULL。
        /// </summary>
        private async Task<SysAiChatResultDto> FinishTurnAsync(AiChatSession session, bool isNewSession, long userId,
            string userMessage, string reply, string model,
            bool hasUsage, int totalPromptTokens, int totalCompletionTokens, int totalTokens,
            List<AiChartViewDto> charts = null, IReadOnlyList<string> storeImageUrls = null, List<AiChatToolCallDto> tools = null)
        {
            var userJson = AiChatImageHelper.ToImagesDataJson(storeImageUrls);
            var dataJson = AiChartAssembler.ToDataJson(charts, tools);

            // 用静态调用而非 Title.IsNullOrEmpty() 扩展：该扩展由 Microsoft.IdentityModel.Tokens 提供，
            // 引入 Fido2（要求 IdentityModel >= 8.2.0）后不再对外公开，升级后这里会编译失败
            var needAutoTitle = string.IsNullOrEmpty(session.Title) || session.Title == "新对话";
            var newTitle = session.Title;
            if (needAutoTitle)
            {
                var titleSrc = storeImageUrls != null && storeImageUrls.Count > 0 &&
                    (string.IsNullOrWhiteSpace(userMessage) || userMessage == AiChatImageHelper.DefaultUserPrompt)
                    ? "图片问答"
                    : userMessage;
                newTitle = AiHelper.AutoTitle(titleSrc);
            }

            // 用户/助手消息与标题更新必须同成败，否则会留下没有回复的孤立用户消息
            await Context.Ado.BeginTranAsync();
            try
            {
                await SaveMessageAsync(session.SessionId, userId, "user", userMessage, model, dataJson: userJson);
                await SaveMessageAsync(session.SessionId, userId, "assistant", reply, model, totalPromptTokens, totalCompletionTokens, totalTokens, dataJson);
                await UpdateAsync(s => s.SessionId == session.SessionId,
                    s => new AiChatSession { Title = newTitle, UpdateTime = DateTime.Now });
                await Context.Ado.CommitTranAsync();
            }
            catch (Exception ex)
            {
                await Context.Ado.RollbackTranAsync();
                _logger.Error(ex, $"AI 对话落库失败已回滚 sessionId={session.SessionId} userId={userId}");
                throw;
            }

            return new SysAiChatResultDto
            {
                SessionId = session.SessionId,
                Title = newTitle,
                Model = model,
                Reply = reply,
                IsNewSession = isNewSession,
                PromptTokens = hasUsage ? totalPromptTokens : null,
                CompletionTokens = hasUsage ? totalCompletionTokens : null,
                TotalTokens = hasUsage ? totalTokens : null,
                Charts = charts,
                Tools = tools
            };
        }

        #endregion 对话编排

        #region 工具定义与执行

        /// <summary>
        /// 全部工具定义（静态）：各模块 Provider 注册的工具（内置办工工具已在 <c>AiOfficeToolProvider</c> 注册）。
        /// 模型 function calling 与前端工具清单（GET aichat/tools）共用此集合，避免两处各维护一份导致漂移。
        /// </summary>
        private List<AiToolDef> BuildToolDefs()
        {
            return _providerToolDefs.ToList();
        }

        /// <summary>
        /// 组装发给模型的 function calling 工具数组。
        /// 只取 Name/Description/Parameters——Label/Permission 属前端展示与清单过滤字段，不参与模型请求。
        /// 与 GET aichat/tools 共用同一份权限过滤结果：无权限的工具既不出现在清单里，也不会下发给模型，
        /// 避免"清单隐藏、模型仍可调用"的口径不一致。
        /// </summary>
        private object[] BuildToolObjects(long userId)
        {
            return GetAuthorizedToolDefs(userId)
                .Select(def => (object)new
                {
                    type = "function",
                    function = new { name = def.Name, description = def.Description, parameters = def.Parameters }
                })
                .ToArray();
        }

        /// <summary>
        /// 当前用户可用的工具定义（内置工具 + 通过权限校验的扩展工具）。
        /// 模型 function calling 与前端工具清单（GET aichat/tools）共用此集合，避免两处过滤口径漂移；
        /// 声明了 Permission 的工具仅对有权限用户返回，权限计算失败按无权限处理（不下发受限工具）。
        /// </summary>
        private List<AiToolDef> GetAuthorizedToolDefs(long userId, bool personalized = false)
        {
            var defs = personalized ? BuildToolDefsForUser(userId) : BuildToolDefs();
            var items = new List<AiToolDef>(defs.Count);
            // 按需加载：只有存在受限工具时才查权限，普通用户不产生额外开销；
            // 权限计算失败（返回 null）只查一次，并按"不可用"处理（fail-closed），不下发受限工具
            List<string> perms = null;
            var permsLoaded = false;
            foreach (var def in defs)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.Name)) continue;
                if (!string.IsNullOrWhiteSpace(def.Permission))
                {
                    if (!permsLoaded)
                    {
                        perms = AiPermissionHelper.TryLoadPerms(_permissionService, userId);
                        permsLoaded = true;
                    }
                    if (!AiPermissionHelper.HasPerm(perms, def.Permission)) continue;
                }
                items.Add(def);
            }
            return items;
        }

        /// <summary>
        /// 按当前用户定制后的工具定义（各 Provider 的 GetToolDefsForUser）。
        /// 仅用于前端工具清单——需要按权限定制示例问句（如 chart 只给有权数据集的示例）；
        /// 模型 function calling 仍走静态 BuildToolDefs()，避免每次请求重复按用户计算与权限查询。
        /// 去重口径与构造时一致：同名以首个为准。
        /// </summary>
        private List<AiToolDef> BuildToolDefsForUser(long userId)
        {
            var defs = new List<AiToolDef>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in _toolProviders)
            {
                var providerDefs = provider.GetToolDefsForUser(userId);
                if (providerDefs == null) continue;
                foreach (var def in providerDefs)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.Name)) continue;
                    if (!seen.Add(def.Name)) continue;
                    defs.Add(def);
                }
            }
            return defs;
        }

        /// <summary>
        /// 当前用户可见的工具清单（GET aichat/tools）：展示名 + 空态引导示例问句。
        /// 不含 Description，避免把工具能力细节透给前端；Samples 按用户权限定制（如 chart 只给有权数据集的示例）；
        /// 与下发给模型的工具数组同源（GetAuthorizedToolDefs），保证前端所见即模型可用。
        /// </summary>
        public Task<List<AiToolCatalogItemDto>> GetMyToolCatalogAsync(long userId)
        {
            var items = GetAuthorizedToolDefs(userId, personalized: true)
                .Select(def => new AiToolCatalogItemDto { Name = def.Name, Label = def.Label, Samples = def.Samples })
                .ToList();
            return Task.FromResult(items);
        }

        /// <summary>
        /// 全量 AI 工具目录（管理端排查用）：各模块 Provider 注册的全部工具，<b>不做权限过滤</b>。
        /// 与 GetMyToolCatalogAsync 同源于工具定义（"目录所见即系统已注册"），差异仅在过滤与字段：
        /// 本方法返回全量并附带 Provider/Permission/Description，便于定位工具归属与实现模块。
        /// 走静态定义（非按用户定制），示例问句为各 Provider 的默认值。
        /// </summary>
        public Task<List<AiToolCatalogDetailDto>> GetToolCatalogAsync()
        {
            var items = new List<AiToolCatalogDetailDto>();
            foreach (var def in BuildToolDefs())
            {
                if (def == null || string.IsNullOrWhiteSpace(def.Name)) continue;
                items.Add(new AiToolCatalogDetailDto
                {
                    Name = def.Name,
                    Label = def.Label,
                    Provider = _toolProviderMap.TryGetValue(def.Name, out var owner) ? owner.ProviderName : "unknown",
                    Permission = def.Permission,
                    Description = def.Description,
                    Samples = def.Samples
                });
            }
            return Task.FromResult(items);
        }

        /// <summary>
        /// 执行指定工具：按工具名路由到对应的注册 Provider（含内置办工工具）。
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="arguments">工具参数</param>
        /// <param name="userId">用户ID</param>
        /// <param name="sessionId">会话ID</param>
        /// <returns>工具执行结果</returns>
        private async Task<AiToolExecResult> ExecuteToolAsync(string toolName, string arguments, long userId, long sessionId)
        {
            if (_toolProviderMap.TryGetValue(toolName ?? string.Empty, out var provider))
            {
                var exec = await provider.ExecuteAsync(toolName, arguments, userId);
                if (exec != null)
                {
                    return exec;
                }
            }
            return AiToolExecResult.Error($"未知工具：{toolName}");
        }

        #endregion 工具定义与执行

        #region helpers

        private sealed class ChatContext
        {
            public string Message { get; init; }
            public AiOptions Options { get; init; }
            public string Model { get; init; }
            public bool IsNewSession { get; init; }
            public AiChatSession Session { get; init; }
            public List<object> Messages { get; init; }
            public object[] Tools { get; init; }
            public int HistoryCount { get; init; }
            public bool UseVision { get; init; }
            public List<string> ModelImageUrls { get; init; }
            public List<string> StoreImageUrls { get; init; }
            public string VisionSystemPrompt { get; init; }
            public string VisionUserPrompt { get; init; }
        }

        /// <summary>
        /// 准备聊天上下文：会话解析/创建 + 历史消息回灌 + 系统提示 + 工具定义；
        /// 有图时切视觉模型，不组装工具。
        /// </summary>
        private async Task<ChatContext> PrepareChatContextAsync(long sessionId, long userId, string message, IReadOnlyList<string> imageUrls)
        {
            var storeImages = AiChatImageHelper.NormalizeClientUrls(imageUrls);
            message = (message ?? "").Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                if (storeImages.Count == 0)
                {
                    throw new Exception("消息内容不能为空");
                }
                message = AiChatImageHelper.DefaultUserPrompt;
            }
            if (message.Length > MaxUserMessageLength)
            {
                throw new Exception($"消息内容过长，最多 {MaxUserMessageLength} 字");
            }

            var options = AiHelper.EnsureAiEnabled();
            var resolved = _llm.ResolveProvider(options);
            var model = string.IsNullOrWhiteSpace(resolved.Model) ? options.Model : resolved.Model;

            var isNewSession = sessionId <= 0;
            var session = await ResolveSessionAsync(sessionId, userId, isNewSession);

            if (storeImages.Count > 0)
            {
                var vision = _llm.ResolveVisionProvider(options);
                if (string.IsNullOrWhiteSpace(vision.Model))
                {
                    throw new Exception("未配置视觉模型。请在当前 Provider 的 VisionModel 中指定支持多模态的模型，或联系管理员配置。");
                }
                // 图片压缩能力已移除（SkiaSharp native 依赖体积过大）：直接把原图转成模型可用的 URL/data URI
                var modelImages = AiChatImageHelper.ToModelImageUrls(storeImages, _env?.WebRootPath, options);
                var (visionSystem, visionUser, historyCount) = await BuildVisionPromptsAsync(session.SessionId, userId, message, storeImages.Count);
                return new ChatContext
                {
                    Message = message,
                    Options = options,
                    Model = vision.Model,
                    IsNewSession = isNewSession,
                    Session = session,
                    Messages = [],
                    Tools = [],
                    HistoryCount = historyCount,
                    UseVision = true,
                    ModelImageUrls = modelImages,
                    StoreImageUrls = storeImages,
                    VisionSystemPrompt = visionSystem,
                    VisionUserPrompt = visionUser
                };
            }

            var (messages, textHistoryCount) = await BuildConversationMessagesAsync(session.SessionId, userId, message);

            return new ChatContext
            {
                Message = message,
                Options = options,
                Model = model,
                IsNewSession = isNewSession,
                Session = session,
                Messages = messages,
                Tools = BuildToolObjects(userId),
                HistoryCount = textHistoryCount
            };
        }

        private async Task<(string SystemPrompt, string UserPrompt, int HistoryCount)> BuildVisionPromptsAsync(
            long sessionId, long userId, string message, int imageCount)
        {
            var text = AiHelper.LoadPrompt("system/ai-chat-vision.md");
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("AI 助手提示词文件缺失：system/ai-chat-vision.md（请检查 Prompts 目录）");
            }
            var system = PromptLoader.Render(text, new Dictionary<string, string>
            {
                ["now"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ["userId"] = userId.ToString()
            });

            var (recent, summaryAnchor) = await ResolveHistoryAsync(sessionId, userId);

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(summaryAnchor))
            {
                sb.AppendLine("【历史对话摘要】");
                sb.AppendLine(summaryAnchor);
                sb.AppendLine();
            }
            if (recent.Count > 0)
            {
                sb.AppendLine("近期对话（仅文本，不含历史图片）：");
                foreach (var h in recent)
                {
                    sb.AppendLine($"{h.Role}: {AiHelper.ClipText(h.Content, 500)}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("用户本轮问题：");
            sb.AppendLine(message);
            sb.AppendLine();
            sb.AppendLine($"用户附带了 {imageCount} 张图片，请结合图片作答。本轮无法调用系统工具。");
            return (system, sb.ToString(), recent.Count);
        }

        /// <summary>
        /// 解析会话：新会话则创建，会话ID>0则查询并验证归属
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="userId"></param>
        /// <param name="isNewSession"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<AiChatSession> ResolveSessionAsync(long sessionId, long userId, bool isNewSession)
        {
            if (isNewSession)
            {
                var session = new AiChatSession { UserId = userId, Title = "新对话" };
                session.SessionId = await Context.Insertable(session).ExecuteReturnSnowflakeIdAsync();
                return session;
            }

            return await Queryable()
                .Where(m => m.SessionId == sessionId && m.UserId == userId)
                .FirstAsync() ?? throw new Exception("会话不存在或无权访问");
        }

        /// <summary>
        /// 构建对话消息列表：系统提示 + 历史消息 + 本轮用户消息
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="userId"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task<(List<object> Messages, int HistoryCount)> BuildConversationMessagesAsync(long sessionId, long userId, string message)
        {
            var messages = new List<object>();

            var (recent, summaryAnchor) = await ResolveHistoryAsync(sessionId, userId);
            var systemPrompt = BuildSystemPrompt(userId);
            if (!string.IsNullOrWhiteSpace(summaryAnchor))
            {
                // 摘要并入主系统提示（而非追加第二条 system 消息），兼容只接受单条 system 的网关，
                // 用于弥补被滑窗丢弃的早期上下文
                systemPrompt = systemPrompt + "\n\n【历史对话摘要（更早的对话要点）】\n" + summaryAnchor;
            }
            messages.Add(new { role = "system", content = systemPrompt });

            foreach (var h in recent)
            {
                messages.Add(new { role = h.Role, content = AiHelper.ClipText(h.Content, 2000) });
            }
            messages.Add(new { role = "user", content = message });
            return (messages, recent.Count);
        }

        /// <summary>
        /// 解析回灌历史：最近 <see cref="MaxHistoryMessages"/> 条保留原文，窗口之前的消息由「水位线 + 增量摘要」压缩。
        /// <para>
        /// 不再按固定条数 <c>Take</c> 截断历史（超出部分曾会被静默丢弃）：只读水位线之后的消息，
        /// 每轮把新滑出窗口的少量旧消息并入上次摘要，摘要随会话持续推进，覆盖全部历史。
        /// 摘要失败自动降级为仅窗口，不阻断对话。
        /// </para>
        /// </summary>
        /// <returns>(recent 正序消息列表, summaryAnchor 摘要文本，无摘要时为 null)</returns>
        private async Task<(List<AiChatMessage> Recent, string SummaryAnchor)> ResolveHistoryAsync(long sessionId, long userId)
        {
            var watermark = GetSummaryWatermark(sessionId);
            var queryLimit = MaxHistoryMessages + SummaryChunkMessages;

            // 按雪花主键排序：与水位线同源，且 CreateTime 同秒落库时顺序稳定
            var history = await Context.Queryable<AiChatMessage>()
                .Where(m => m.SessionId == sessionId && m.UserId == userId && m.Role != "tool" && m.MessageId > watermark)
                .OrderBy(m => m.MessageId, OrderByType.Desc)
                .Take(queryLimit)
                .ToListAsync();
            history.Reverse();// 正序

            if (history.Count <= MaxHistoryMessages)
            {
                // 窗口装得下：无需新增摘要，但仍要带上水位线之前累积的摘要
                return (history, watermark > 0 ? ReadStoredSummary(sessionId, watermark) : null);
            }

            var recent = history.TakeLast(MaxHistoryMessages).ToList();
            List<AiChatMessage> older;
            if (history.Count < queryLimit)
            {
                // 未读满：窗口外的就是全部未摘要内容，天然与窗口连续
                older = history.Take(history.Count - MaxHistoryMessages).ToList();
            }
            else
            {
                // 读满：说明水位线之后还有更多历史（缓存过期、或首次摘要就是超长会话），
                // 从水位线正序取一段接力摘要，保证摘要连续推进不跳段；本次没覆盖到的留待后续轮次续上
                older = await Context.Queryable<AiChatMessage>()
                    .Where(m => m.SessionId == sessionId && m.UserId == userId && m.Role != "tool" && m.MessageId > watermark)
                    .OrderBy(m => m.MessageId)
                    .Take(SummaryChunkMessages)
                    .ToListAsync();
                // 丢弃与最近窗口重叠的尾部（按主键判断，两边都已正序）
                var windowStart = recent[0].MessageId;
                older = older.Where(m => m.MessageId < windowStart).ToList();
            }

            var summary = await BuildHistorySummaryAsync(sessionId, userId, watermark, older);
            return (recent, summary);
        }

        /// <summary>
        /// 生成（或更新）历史摘要。水位线 = 已摘要到的最后一条 MessageId，它把摘要从「每次全量重算」变成增量更新：
        /// 输入 = 上次摘要 + 本轮新滑出窗口的旧消息，输出覆盖此前全部历史；同一水位线重复请求直接命中缓存、零模型调用。
        /// <para>
        /// 缓存键按水位线分而不是按「窗口外首尾消息」，因为尾消息每轮都会变，旧实现等价于每轮全量重算。
        /// </para>
        /// </summary>
        private async Task<string> BuildHistorySummaryAsync(long sessionId, long userId, long watermark, List<AiChatMessage> older)
        {
            if (older.Count == 0)
            {
                return watermark > 0 ? ReadStoredSummary(sessionId, watermark) : null;
            }

            var cache = CacheStore.For(CacheBackend.Cache);
            var previous = watermark > 0 ? ReadStoredSummary(sessionId, watermark) : null;

            var summaryPrompt = AiHelper.LoadPrompt("system/ai-chat-history-summary.md");
            if (string.IsNullOrWhiteSpace(summaryPrompt))
            {
                // 提示词缺失时静默降级为仅窗口，不影响本轮对话
                _logger.Warn($"AI 历史摘要提示词缺失已跳过摘要：system/ai-chat-history-summary.md sessionId={sessionId}");
                return previous;
            }

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(previous))
            {
                // 增量合并：把「已有摘要」和「新增对话」一起压缩，避免每轮重读整段历史
                sb.AppendLine("【已有摘要（更早的对话要点）】");
                sb.AppendLine(previous);
                sb.AppendLine();
                sb.AppendLine("【新增对话】");
            }
            foreach (var m in older)
            {
                sb.AppendLine($"{m.Role}: {AiHelper.ClipText(m.Content, 800)}");
            }

            try
            {
                var options = AiHelper.EnsureAiEnabled();
                var summary = await _llm.ChatAsync(options, summaryPrompt, sb.ToString(), AiSceneCatalog.AiChat);
                summary = (summary ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    var covered = older[^1].MessageId;
                    cache.Set(SummaryCacheKey(sessionId, covered), summary, SummaryCacheMinutes);
                    cache.Set(SummaryHeadKey(sessionId), covered, SummaryCacheMinutes);
                    // 只保留最新水位线的摘要文本，避免长会话在缓存里堆积一串历史版本
                    if (watermark > 0 && watermark != covered) cache.Remove(SummaryCacheKey(sessionId, watermark));
                    _logger.Info($"AI 历史摘要已更新 sessionId={sessionId} userId={userId} 本轮旧消息={older.Count} 覆盖到={covered} 增量={!string.IsNullOrWhiteSpace(previous)} len={summary.Length}");
                    return summary;
                }
            }
            catch (Exception ex)
            {
                // 摘要失败绝不阻断本轮对话，降级为仅窗口（有旧摘要时继续沿用）
                _logger.Warn(ex, $"AI 历史摘要生成失败已降级为仅窗口 sessionId={sessionId} userId={userId} 本轮旧消息={older.Count}");
            }
            return previous;
        }

        /// <summary>摘要水位线（已摘要到的最后一条 MessageId）；读取失败按无摘要处理，不影响对话</summary>
        private static long GetSummaryWatermark(long sessionId)
        {
            try
            {
                return CacheStore.For(CacheBackend.Cache).Get<long?>(SummaryHeadKey(sessionId)) ?? 0;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"AI 历史摘要水位线读取失败，按无摘要处理 sessionId={sessionId}");
                return 0;
            }
        }

        /// <summary>读水位线对应的摘要文本；缺失（缓存过期/被清理）返回 null</summary>
        private static string ReadStoredSummary(long sessionId, long coveredId)
        {
            if (coveredId <= 0) return null;
            try
            {
                return CacheStore.For(CacheBackend.Cache).Get<string>(SummaryCacheKey(sessionId, coveredId));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"AI 历史摘要读取失败 sessionId={sessionId} covered={coveredId}");
                return null;
            }
        }

        /// <summary>摘要文本键：按水位线分键，同一水位线（重试/重放）稳定命中</summary>
        private static string SummaryCacheKey(long sessionId, long coveredId)
            => $"ai:history:summary:{App.GetCurrentTenantId() ?? "0"}:{sessionId}:{coveredId}";

        /// <summary>水位线键：指向当前摘要覆盖到的 MessageId</summary>
        private static string SummaryHeadKey(long sessionId)
            => $"ai:history:head:{App.GetCurrentTenantId() ?? "0"}:{sessionId}";

        private static void AccumulateRoundUsage(AiLlmClient.ChatToolResult turn, int round, List<string> roundDiag,
            ref bool hasUsage, ref int totalPromptTokens, ref int totalCompletionTokens, ref int totalTokens)
        {
            totalPromptTokens += turn.PromptTokens;
            totalCompletionTokens += turn.CompletionTokens;
            totalTokens += turn.TotalTokens;
            if (totalTokens > 0) hasUsage = true;

            var contentLen = turn.Content?.Length ?? 0;
            var toolNames = turn.ToolCalls?.Select(x => x.Name) ?? new List<string>();
            roundDiag.Add($"r{round + 1}:finish={turn.FinishReason ?? "null"},contentLen={contentLen},tools=[{string.Join(",", toolNames)}]");
        }

        /// <summary>
        /// 单次对话的编排状态（ChatAsync / StreamChatAsync 共用）：用量、工具轨迹、图表与失败态。
        /// </summary>
        private sealed class ChatRoundState
        {
            public readonly List<AiChartQueryResult> ChartQueries = new();
            public readonly List<AiChatToolCallDto> ToolTrace = new();
            public readonly List<string> RoundDiag = new();
            public bool HasUsage;
            public int TotalPromptTokens;
            public int TotalCompletionTokens;
            public int TotalTokens;
            public bool LastRoundAllFailed;
            public bool ForceRetryUsed;
        }

        private static void RecordRoundUsage(ChatRoundState st, AiLlmClient.ChatToolResult turn, int round)
        {
            AccumulateRoundUsage(turn, round, st.RoundDiag,
                ref st.HasUsage, ref st.TotalPromptTokens, ref st.TotalCompletionTokens, ref st.TotalTokens);
        }

        /// <summary>本轮无工具调用时：上一轮工具全失败且尚未强制重试过，则追加提醒并返回 true（调用方应 continue）</summary>
        private static bool TryConsumeForceRetry(ChatRoundState st, int round, List<object> messages)
        {
            if (!ShouldForceToolRetry(st.LastRoundAllFailed, st.ForceRetryUsed, round)) return false;
            st.ForceRetryUsed = true;
            AppendToolRetryReminder(messages);
            return true;
        }

        /// <summary>
        /// 执行本轮全部工具调用：回灌 assistant 工具请求与各工具结果，记录轨迹与失败态，返回按调用顺序的执行结果。
        /// 同一轮多个工具互不依赖：并行执行降低首字延迟，结果按 call.Id 关联、回灌顺序固定。
        /// 前提：现有工具均为只读查询、不开事务，且连接 IsAutoCloseConnection=true（每次操作独立开关连接）；
        /// 若新增工具写库/开事务，或关闭自动关闭连接，必须改回串行执行。
        /// </summary>
        private async Task<AiToolExecResult[]> ExecuteRoundToolsAsync(ChatRoundState st, AiLlmClient.ChatToolResult turn,
            List<object> messages, long userId, long sessionId)
        {
            AppendAssistantToolCalls(messages, turn);
            var outcomes = await Task.WhenAll(turn.ToolCalls.Select(c => ExecuteToolSafelyAsync(c, userId, sessionId)));
            var execs = new AiToolExecResult[outcomes.Length];
            var allFailed = true;
            for (var i = 0; i < turn.ToolCalls.Count; i++)
            {
                var call = turn.ToolCalls[i];
                var exec = outcomes[i].Result;
                execs[i] = exec;
                st.ToolTrace.Add(new AiChatToolCallDto { Name = call.Name, Ok = exec.Ok });
                if (exec.Ok) allFailed = false;
                AppendToolResult(messages, call.Id, exec, st.ChartQueries);
            }
            st.LastRoundAllFailed = allFailed;

            // 审计落库串行放在并行执行之后：同一 SqlSugar 连接不支持并发命令，且审计失败不能
            // 影响已经回灌给模型的工具结果（RecordAsync 内部吞异常只记 Warn）。
            await _toolCallLogService.RecordAsync(outcomes.Select(o => o.Log).ToList());
            return execs;
        }

        /// <summary>
        /// 把模型返回的助手消息和工具调用信息追加到消息列表中，供下一轮模型调用使用。
        /// </summary>
        /// <param name="messages">消息列表</param>
        /// <param name="turn">模型返回的助手消息和工具调用信息</param>
        private static void AppendAssistantToolCalls(List<object> messages, AiLlmClient.ChatToolResult turn)
        {
            messages.Add(new
            {
                role = "assistant",
                content = turn.Content ?? "",
                tool_calls = turn.ToolCalls.Select(c => new
                {
                    id = c.Id,
                    type = "function",
                    function = new { name = c.Name, arguments = c.Arguments }
                }).ToArray()
            });
        }

        /// <summary>
        /// 把工具执行结果追加到消息列表中，供下一轮模型调用使用。
        /// </summary>
        /// <param name="messages"></param>
        /// <param name="toolCallId"></param>
        /// <param name="exec"></param>
        /// <param name="chartQueries"></param>
        private static void AppendToolResult(List<object> messages, string toolCallId, AiToolExecResult exec, List<AiChartQueryResult> chartQueries)
        {
            var content = exec.Content ?? "";
            if (!exec.Ok)
            {
                content = $"[错误] {content}\n请按该工具 schema 修正参数后再次调用，不要把本错误原文当作最终答复。";
            }
            else
            {
                // 成功结果含日程标题、日志内容等用户可控文本：包裹为数据区并转义同名标签，防止伪造闭合标签逃逸
                content = WrapUntrustedToolData(content);
                if (exec.ChartQuery != null)
                {
                    chartQueries.Add(exec.ChartQuery);
                }
            }
            messages.Add(new { role = "tool", tool_call_id = toolCallId, content = AiHelper.ClipText(content, ToolResultMaxLen) });
        }

        private const string ToolDataTag = "tool_result";

        private static string WrapUntrustedToolData(string content)
        {
            var escaped = content
                .Replace("<" + ToolDataTag, "&lt;" + ToolDataTag, StringComparison.OrdinalIgnoreCase)
                .Replace("</" + ToolDataTag, "&lt;/" + ToolDataTag, StringComparison.OrdinalIgnoreCase);
            return $"<{ToolDataTag}>\n{escaped}\n</{ToolDataTag}>";
        }

        /// <summary>
        /// 安全执行工具：执行前按工具粒度的权限码二次拦截，捕获异常并记录日志，
        /// 同时产出一条可落库的审计记录（参数摘要/结果状态/耗时）。
        /// <para>
        /// 为什么执行期还要再判一次权限：工具清单虽已按权限过滤后再下发给模型，但提示词注入或模型幻觉
        /// 仍可能直接点名未下发的受限工具；Controller 只能按接口授权，无法表达"工具粒度"。
        /// 这里是唯一能拦住的收口点，拦下时状态记为 denied，治理页据此排查越权尝试。
        /// </para>
        /// </summary>
        /// <param name="call"></param>
        /// <param name="userId"></param>
        /// <param name="sessionId"></param>
        /// <returns>工具执行结果 + 对应审计记录</returns>
        private async Task<ToolCallOutcome> ExecuteToolSafelyAsync(AiLlmClient.ToolCall call, long userId, long sessionId)
        {
            var toolName = call.Name ?? string.Empty;
            _toolDefMap.TryGetValue(toolName, out var def);
            _toolProviderMap.TryGetValue(toolName, out var provider);
            var log = NewToolCallLog(toolName, def, provider, call.Arguments, userId, sessionId);

            var stopwatch = Stopwatch.StartNew();
            AiToolExecResult result;
            try
            {
                var denyMessage = CheckToolPermission(def, userId, out var permCalcFailed);
                if (denyMessage != null)
                {
                    result = AiToolExecResult.Error(denyMessage);
                    log.Status = "denied";
                    log.Success = 0;
                    log.ErrorMsg = permCalcFailed ? "权限计算失败，按无权限处理" : $"缺少权限：{def.Permission}";
                }
                else
                {
                    result = await ExecuteToolAsync(toolName, call.Arguments, userId, sessionId);
                    log.Status = ResolveToolStatus(provider, result);
                    log.Success = result?.Ok == true ? 1 : 0;
                    if (result?.Ok != true) log.ErrorMsg = AiHelper.Truncate(result?.Content ?? "", 1000);
                }
            }
            catch (Exception ex)
            {
                // 详情只写后端日志便于排查；回灌给模型的是通用失败提示，
                // 避免把内部异常细节经模型转述暴露给前端。
                _logger.Error($"AI 工具执行异常 tool={toolName} args={AiHelper.ClipText(MaskSensitiveArgs(call.Arguments), 300)} err={ex}");
                result = AiToolExecResult.Error("该操作执行失败，请告知用户稍后重试");
                log.Status = "exception";
                log.Success = 0;
                log.ErrorMsg = AiHelper.Truncate(ex.Message, 1000);
            }
            finally
            {
                stopwatch.Stop();
                log.DurationMs = stopwatch.ElapsedMilliseconds;
            }

            return new ToolCallOutcome { Result = result, Log = log };
        }

        /// <summary>
        /// 执行期工具鉴权：只有声明了 Permission 的工具才判（未声明的靠 Provider 内部按 userId 隔离数据）。
        /// 权限计算失败按"不可用"处理（fail-closed），并通过 <paramref name="permCalcFailed"/> 区分
        /// "无权限"（联系管理员授权）与"无法校验"（稍后重试）——两者给用户的行动指引不同。
        /// </summary>
        /// <returns>null 表示放行；非 null 为回灌给模型的拒绝提示</returns>
        private string CheckToolPermission(AiToolDef def, long userId, out bool permCalcFailed)
        {
            permCalcFailed = false;
            var permission = def?.Permission;
            if (string.IsNullOrWhiteSpace(permission)) return null;

            // 本实例 = 单次请求（Transient），同一请求内工具调用共享一次权限查询
            if (!_callPermsLoaded)
            {
                _callPerms = AiPermissionHelper.TryLoadPerms(_permissionService, userId);
                _callPermsLoaded = true;
            }
            if (AiPermissionHelper.HasPerm(_callPerms, permission)) return null;

            permCalcFailed = _callPerms == null;
            return permCalcFailed
                ? "暂时无法校验当前账号权限，请稍后重试"
                : "当前账号无权调用该工具，请联系管理员授权后重试";
        }

        /// <summary>结果状态归类：工具未注册为 unknown_tool，执行成功 success，业务失败 failed</summary>
        private static string ResolveToolStatus(IAiAssistantToolProvider provider, AiToolExecResult result)
        {
            if (provider == null) return "unknown_tool";
            return result?.Ok == true ? "success" : "failed";
        }

        /// <summary>构造一条工具调用审计（身份只取可信来源：登录上下文优先，回退编排入参的 userId）</summary>
        private static AiToolCallLog NewToolCallLog(string toolName, AiToolDef def, IAiAssistantToolProvider provider,
            string arguments, long userId, long sessionId)
        {
            var user = App.HttpContext?.GetCurrentUser();
            return new AiToolCallLog
            {
                Scene = "ai_chat",
                SessionId = sessionId,
                // 入库字段用 Truncate（精确截断，不追加省略号），保证不超过列宽
                ToolName = AiHelper.Truncate(toolName, 100),
                Provider = AiHelper.Truncate(provider?.ProviderName, 64),
                Permission = AiHelper.Truncate(def?.Permission, 128),
                ArgsSummary = AiHelper.Truncate(MaskSensitiveArgs(arguments), 500),
                // 占位值，所有分支都会在落库前覆盖（异常分支 = exception）
                Status = "unknown",
                Success = 0,
                UserId = user?.UserId > 0 ? user.UserId : userId,
                UserName = AiHelper.Truncate(user?.UserName, 64),
                TenantId = AiHelper.Truncate(App.GetCurrentTenantId(), 64),
                TraceId = AiHelper.Truncate(App.HttpContext?.TraceIdentifier, 64)
            };
        }

        /// <summary>
        /// 参数敏感字段掩码：工具参数由模型生成，可能夹带密码/令牌/手机号等，落库前统一打码，
        /// 只保留"调用了什么工具、带了哪些字段"的排查价值。
        /// </summary>
        private static string MaskSensitiveArgs(string argsJson)
        {
            if (string.IsNullOrWhiteSpace(argsJson)) return argsJson;
            return SensitiveArgRegex.Replace(argsJson, m => $"\"{m.Groups["key"].Value}\":\"***\"");
        }

        /// <summary>匹配 JSON 中 key 含敏感词（password/token/mobile 等）的字符串值</summary>
        private static readonly Regex SensitiveArgRegex = new(
            "\"(?<key>[A-Za-z0-9_]*(?:password|pwd|passwd|token|secret|apikey|api_key|mobile|phone|email|idcard|cardno)[A-Za-z0-9_]*)\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>工具执行结果 + 对应审计记录（审计统一在并行执行结束后串行落库）</summary>
        private sealed class ToolCallOutcome
        {
            public AiToolExecResult Result { get; init; }
            public AiToolCallLog Log { get; init; }
        }

        private static bool ShouldForceToolRetry(bool lastRoundAllFailed, bool forceRetryUsed, int round)
        {
            return lastRoundAllFailed && !forceRetryUsed && round + 1 < MaxToolRounds;
        }

        private static void AppendToolRetryReminder(List<object> messages)
        {
            messages.Add(new { role = "user", content = ToolRetryReminder });
        }

        private static string BuildSystemPrompt(long userId)
        {
            var text = AiHelper.LoadPrompt("system/ai-chat-system.md");
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("AI 助手提示词文件缺失：system/ai-chat-system.md（请检查 Prompts 目录）");
            }
            return PromptLoader.Render(text, new Dictionary<string, string>
            {
                ["now"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ["userId"] = userId.ToString()
            });
        }

        #endregion helpers
    }
}
