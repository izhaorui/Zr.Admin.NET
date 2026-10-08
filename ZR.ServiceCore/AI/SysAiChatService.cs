using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using NLog;
using System.Runtime.CompilerServices;
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
        private readonly ISysAiService _sysAi;
        private readonly IDailyScheduleService _scheduleService;
        private readonly IAiChatLlmGateway _llm;
        private readonly ISysPermissionService _permissionService;
        private readonly IReadOnlyList<IAiAssistantToolProvider> _toolProviders;
        private readonly IReadOnlyList<AiToolDef> _providerToolDefs;
        private readonly Dictionary<string, IAiAssistantToolProvider> _toolProviderMap;
        private readonly IWebHostEnvironment _env;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>最大工具轮询次数（防止模型反复要求调用工具造成死循环）</summary>
        private const int MaxToolRounds = 5;
        private const string ToolRetryReminder =
            "上一轮工具全部失败。请根据错误与工具 schema 修正参数后再次调用；若确实无法调用，再向用户简要说明原因，不要复述内部错误原文。";
        /// <summary>回灌历史消息最近窗口条数（窗口内保留原文，窗口外用摘要锚点压缩）</summary>
        private const int MaxHistoryMessages = 20;
        /// <summary>历史摘要单次最多纳入的旧消息条数（防超长会话一次性摘要烧 token）</summary>
        private const int SummaryMaxOlderMessages = 100;
        /// <summary>历史摘要缓存分钟数（窗口边界每约 20 轮才移动，期间命中缓存）</summary>
        private const int SummaryCacheMinutes = 30;
        /// <summary>单条工具结果回灌给模型的最大长度</summary>
        private const int ToolResultMaxLen = 8000;
        /// <summary>单条用户消息最大长度（与请求 DTO 校验一致，服务端兜底防超长文本烧 token）</summary>
        private const int MaxUserMessageLength = 2000;

        /// <summary>内置工具名（工具名全局唯一：与 Provider 注册的同名工具冲突时以内置为准）。</summary>
        private const string ToolQueryMySchedules = "query_my_schedules";
        private const string ToolParseSchedule = "parse_schedule";
        private const string ToolGenerateWeeklyReport = "generate_weekly_report";

        private static readonly HashSet<string> BuiltInToolNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ToolQueryMySchedules, ToolParseSchedule, ToolGenerateWeeklyReport
        };

        /// <param name="sysAi">系统 AI 服务（日程解析/周报）</param>
        /// <param name="scheduleService">日程服务（日程查询工具）</param>
        /// <param name="llm">大模型调用网关（封装静态 AiLlmClient，便于测试替换）</param>
        /// <param name="toolProviders">各模块注册的扩展工具提供者</param>
        /// <param name="permissionService">权限服务（工具清单按用户权限过滤）</param>
        /// <param name="env">宿主环境（本机上传图片转 data URI 时定位 wwwroot）</param>
        public SysAiChatService(ISysAiService sysAi, IDailyScheduleService scheduleService,
            IAiChatLlmGateway llm, IEnumerable<IAiAssistantToolProvider> toolProviders,
            ISysPermissionService permissionService, IWebHostEnvironment env)
        {
            _sysAi = sysAi;
            _scheduleService = scheduleService;
            _llm = llm;
            _permissionService = permissionService;
            _env = env;
            _toolProviders = (toolProviders ?? []).ToList();

            var defs = new List<AiToolDef>();
            _toolProviderMap = new Dictionary<string, IAiAssistantToolProvider>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in _toolProviders)
            {
                var providerDefs = provider.GetToolDefs();
                if (providerDefs == null || providerDefs.Count == 0) continue;
                foreach (var def in providerDefs)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.Name)) continue;
                    // 工具名全局唯一（CONTRIBUTING 约定）。重复注册若静默保留，同一工具会在前端清单
                    // 与模型请求里各出现两次，且模型调用时归属不确定，因此丢弃并告警。
                    if (BuiltInToolNames.Contains(def.Name))
                    {
                        _logger.Warn($"AI 扩展工具名与内置工具重复已忽略：{def.Name}（提供者 {provider.ProviderName}）");
                        continue;
                    }
                    if (_toolProviderMap.TryGetValue(def.Name, out var owner))
                    {
                        _logger.Warn($"AI 工具名重复已忽略：{def.Name}（已由 {owner.ProviderName} 注册，跳过 {provider.ProviderName} 的重复定义）");
                        continue;
                    }
                    defs.Add(def);
                    _toolProviderMap.Add(def.Name, provider);
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
        public async Task<SysAiChatDetailDto> GetMessagesAsync(long sessionId, long userId)
        {
            var session = await Queryable()
                .Where(m => m.SessionId == sessionId && m.UserId == userId)
                .FirstAsync();
            if (session == null)
            {
                throw new Exception("会话不存在或无权查看");
            }
            var msgs = await Context.Queryable<AiChatMessage>()
                .Where(m => m.SessionId == sessionId && m.UserId == userId)
                .OrderBy(m => m.CreateTime, OrderByType.Asc)
                .ToListAsync();
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
                Messages = list
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
            string reply = "";
            var chartQueries = new List<AiChartQueryResult>();
            var toolTrace = new List<AiChatToolCallDto>();
            var roundDiag = new List<string>();
            var hasUsage = false;
            int totalPromptTokens = 0, totalCompletionTokens = 0, totalTokens = 0;
            var lastRoundAllFailed = false;
            var forceRetryUsed = false;
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

                AccumulateRoundUsage(turn, round, roundDiag,
                    ref hasUsage, ref totalPromptTokens, ref totalCompletionTokens, ref totalTokens);

                var hasToolCalls = turn.ToolCalls != null && turn.ToolCalls.Count > 0;
                if (!hasToolCalls)
                {
                    if (ShouldForceToolRetry(lastRoundAllFailed, forceRetryUsed, round))
                    {
                        forceRetryUsed = true;
                        AppendToolRetryReminder(context.Messages);
                        continue;
                    }
                    reply = turn.Content ?? "";
                    break;
                }

                AppendAssistantToolCalls(context.Messages, turn);
                var allFailed = true;
                // 同一轮多个工具互不依赖：并行执行降低首字延迟，结果按 call.Id 关联、回灌顺序固定。
                // 前提：现有工具均为只读查询、不开事务，且连接 IsAutoCloseConnection=true（每次操作独立开关连接）；
                // 若新增工具写库/开事务，或关闭自动关闭连接，必须改回串行执行。
                var execs = await Task.WhenAll(turn.ToolCalls.Select(c => ExecuteToolSafelyAsync(c, userId, context.Session.SessionId)));
                for (var i = 0; i < turn.ToolCalls.Count; i++)
                {
                    var call = turn.ToolCalls[i];
                    var exec = execs[i];
                    toolTrace.Add(new AiChatToolCallDto { Name = call.Name, Ok = exec.Ok });
                    if (exec.Ok) allFailed = false;
                    AppendToolResult(context.Messages, call.Id, exec, chartQueries);
                }
                lastRoundAllFailed = allFailed;
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                _logger.Error($"AI 回复为空(触发兜底文案) userId={userId} sessionId={context.Session.SessionId} model={context.Model} " +
                    $"msg={AiHelper.ClipText(context.Message, 200)} history={context.HistoryCount} rounds={string.Join(" | ", roundDiag)}");
                reply = "抱歉，我这边没有正常生成回答，请重新描述一下你的问题。";
            }

            var assembled = AiChartAssembler.Assemble(reply, chartQueries);
            reply = assembled.Reply;
            var charts = assembled.Charts;

            // 4. 落库 + 会话元信息维护
            return await FinishTurnAsync(context.Session, context.IsNewSession, userId, context.Message, reply, context.Model,
                hasUsage, totalPromptTokens, totalCompletionTokens, totalTokens, charts, tools: toolTrace);
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
            string reply = "";
            var chartQueries = new List<AiChartQueryResult>();
            var toolTrace = new List<AiChatToolCallDto>();
            var roundDiag = new List<string>();
            var hasUsage = false;
            int totalPromptTokens = 0, totalCompletionTokens = 0, totalTokens = 0;
            var lastRoundAllFailed = false;
            var forceRetryUsed = false;
            for (var round = 0; round < MaxToolRounds; round++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AiLlmClient.ChatToolResult turn = null;
                var bufferDeltas = lastRoundAllFailed;
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
                    throw new HttpRequestException("AI 服务未返回有效响应，请重试");
                }

                AccumulateRoundUsage(turn, round, roundDiag,
                    ref hasUsage, ref totalPromptTokens, ref totalCompletionTokens, ref totalTokens);

                var hasToolCalls = turn.ToolCalls != null && turn.ToolCalls.Count > 0;
                if (!hasToolCalls)
                {
                    if (ShouldForceToolRetry(lastRoundAllFailed, forceRetryUsed, round))
                    {
                        forceRetryUsed = true;
                        AppendToolRetryReminder(context.Messages);
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

                AppendAssistantToolCalls(context.Messages, turn);
                var allFailed = true;
                // 先统一下发 start 事件，再并行执行，最后统一 done：事件顺序与前端契约不变，且降首字延迟
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var call in turn.ToolCalls)
                {
                    yield return new SysAiChatStreamDto { Type = "tool", ToolName = call.Name, ToolStatus = "start" };
                }
                var streamExecs = await Task.WhenAll(turn.ToolCalls.Select(c => ExecuteToolSafelyAsync(c, userId, context.Session.SessionId)));
                for (var i = 0; i < turn.ToolCalls.Count; i++)
                {
                    var call = turn.ToolCalls[i];
                    var exec = streamExecs[i];
                    yield return new SysAiChatStreamDto { Type = "tool", ToolName = call.Name, ToolStatus = "done", ToolOk = exec.Ok };
                    toolTrace.Add(new AiChatToolCallDto { Name = call.Name, Ok = exec.Ok });
                    if (exec.Ok) allFailed = false;
                    AppendToolResult(context.Messages, call.Id, exec, chartQueries);
                }
                lastRoundAllFailed = allFailed;
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                _logger.Error($"AI 回复为空(触发兜底文案) userId={userId} sessionId={context.Session.SessionId} model={context.Model} " +
                    $"msg={AiHelper.ClipText(message, 200)} history={context.HistoryCount} rounds={string.Join(" | ", roundDiag)}");
                reply = "抱歉，我这边没有正常生成回答，请重新描述一下你的问题。";
            }

            var assembledStream = AiChartAssembler.Assemble(reply, chartQueries);
            reply = assembledStream.Reply;
            var chartsStream = assembledStream.Charts;

            // 4. 落库 + 会话元信息维护
            var result = await FinishTurnAsync(context.Session, context.IsNewSession, userId, context.Message, reply, context.Model,
                hasUsage, totalPromptTokens, totalCompletionTokens, totalTokens, chartsStream, tools: toolTrace);
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
            await SaveMessageAsync(session.SessionId, userId, "user", userMessage, model, dataJson: userJson);
            var dataJson = AiChartAssembler.ToDataJson(charts, tools);
            await SaveMessageAsync(session.SessionId, userId, "assistant", reply, model, totalPromptTokens, totalCompletionTokens, totalTokens, dataJson);

            var needAutoTitle = session.Title.IsNullOrEmpty() || session.Title == "新对话";
            var newTitle = session.Title;
            if (needAutoTitle)
            {
                var titleSrc = storeImageUrls != null && storeImageUrls.Count > 0 &&
                    (string.IsNullOrWhiteSpace(userMessage) || userMessage == AiChatImageHelper.DefaultUserPrompt)
                    ? "图片问答"
                    : userMessage;
                newTitle = AiHelper.AutoTitle(titleSrc);
            }
            await UpdateAsync(s => s.SessionId == session.SessionId,
                s => new AiChatSession { Title = newTitle, UpdateTime = DateTime.Now });

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
        /// 全部工具定义：内置办工工具 + 各模块 Provider 注册的扩展工具。
        /// 模型 function calling 与前端工具清单（GET aichat/tools）共用此集合，避免两处各维护一份导致漂移。
        /// 内置工具不声明 Permission：它们只访问“当前登录用户自己的数据”（日程/周报，按 userId 隔离），
        /// 不存在跨用户越权面；若声明权限码而菜单未授权，会把这些工具从所有人的清单里一并隐藏（功能回归）。
        /// 需要按人/按角色控制的内置能力，请改为 Provider 注册并声明 Permission。
        /// </summary>
        private List<AiToolDef> BuildToolDefs()
        {
            var defs = new List<AiToolDef>
            {
                new AiToolDef
                {
                    Name = ToolQueryMySchedules,
                    Label = "日程查询",
                    Description = "查询当前登录用户指定日期范围内的日程安排（按截止/完成/创建时间任一命中，含已完成）。不传参数默认查最近 7 天。用于“我今天有什么安排/本周日程/某天日程”。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["startDate"] = new { type = "string", description = "开始日期，格式 yyyy-MM-dd，可省略" },
                            ["endDate"] = new { type = "string", description = "结束日期（含当天），格式 yyyy-MM-dd，可省略" }
                        },
                        required = Array.Empty<string>()
                    }
                },
                new AiToolDef
                {
                    Name = ToolParseSchedule,
                    Label = "日程解析",
                    Description = "把用户口语化的日程描述解析成结构化日程草稿（标题/内容/优先级/截止/提醒）。只生成草稿不落库。用于“帮我记一个日程：...”等表达。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["text"] = new { type = "string", description = "用户原始口语描述" }
                        },
                        required = new[] { "text" }
                    }
                },
                new AiToolDef
                {
                    Name = ToolGenerateWeeklyReport,
                    Label = "周报生成",
                    Description = "汇总当前用户本周日程自动生成工作周报草稿（Markdown）。用于“帮我写周报/本周总结”。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>(),
                        required = Array.Empty<string>()
                    }
                }
            };
            defs.AddRange(_providerToolDefs);
            return defs;
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
        private List<AiToolDef> GetAuthorizedToolDefs(long userId)
        {
            var defs = BuildToolDefs();
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
        /// 当前用户可见的工具展示名清单（GET aichat/tools）。
        /// 只返回 Name/Label 两个展示字段，不含 Description，避免把工具能力细节透给前端；
        /// 与下发给模型的工具数组同源（GetAuthorizedToolDefs），保证前端所见即模型可用。
        /// </summary>
        public Task<List<AiToolCatalogItemDto>> GetMyToolCatalogAsync(long userId)
        {
            var items = GetAuthorizedToolDefs(userId)
                .Select(def => new AiToolCatalogItemDto { Name = def.Name, Label = def.Label })
                .ToList();
            return Task.FromResult(items);
        }

        /// <summary>
        /// 执行指定工具
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="arguments">工具参数</param>
        /// <param name="userId">用户ID</param>
        /// <param name="sessionId">会话ID</param>
        /// <returns>工具执行结果</returns>
        private async Task<AiToolExecResult> ExecuteToolAsync(string toolName, string arguments, long userId, long sessionId)
        {
            var args = string.IsNullOrWhiteSpace(arguments) ? new JObject() : JObject.Parse(arguments);
            switch (toolName)
            {
                case "query_my_schedules":
                    return await QueryMySchedulesAsync(args, userId);
                case "parse_schedule":
                    return await ParseScheduleAsync(args);
                case "generate_weekly_report":
                    return await GenerateWeeklyReportAsync(userId);
            }

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

        /// <summary>
        /// 查询本人日程
        /// </summary>
        /// <param name="args">查询参数</param>
        /// <param name="userId">用户ID</param>
        /// <returns>工具执行结果</returns>
        private async Task<AiToolExecResult> QueryMySchedulesAsync(JObject args, long userId)
        {
            var startDate = AiHelper.TryParseDate(args["startDate"]?.Value<string>()) ?? DateTime.Today;
            var endDate = AiHelper.TryParseDate(args["endDate"]?.Value<string>()) ?? startDate.AddDays(6);
            endDate = endDate.Date.AddDays(1).AddSeconds(-1);

            var list = await _scheduleService.GetByDateRangeAsync(userId, startDate, endDate);
            if (list == null || list.Count == 0)
            {
                return AiToolExecResult.Success(
                    $"在 {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd} 期间未查询到日程安排。");
            }
            var lines = list.Take(50).Select(s =>
            {
                var status = s.Status == "1" ? "已完成" : "未完成";
                var pri = s.Priority switch { 1 => "低", 3 => "高", _ => "中" };
                var due = s.DueTime?.ToString("MM-dd HH:mm") ?? "无截止";
                var content = string.IsNullOrWhiteSpace(s.Content) ? "" : $" | {AiHelper.ClipText(s.Content, 120)}";
                return $"- [{status}][优先级{pri}] {s.Title}（截止 {due}）{content}";
            });
            var tail = list.Count > 50 ? $"\n（共 {list.Count} 条，仅显示前 50 条）" : "";
            return AiToolExecResult.Success(
                $"查询到 {list.Count} 条日程：\n" + string.Join("\n", lines) + tail);
        }

        /// <summary>
        /// 日程口语解析（草稿，不落库）
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task<AiToolExecResult> ParseScheduleAsync(JObject args)
        {
            var text = args["text"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return AiToolExecResult.Error("缺少待解析的日程描述 text");
            }
            var result = await _sysAi.ParseScheduleAsync(new SysAiScheduleParseInput { Text = text });
            var pri = result.Priority switch { 1 => "低", 3 => "高", _ => "中" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"日程草稿解析完成（未保存，展示给用户确认即可）：");
            sb.AppendLine($"- 标题：{result.Title}");
            if (!string.IsNullOrWhiteSpace(result.Content)) sb.AppendLine($"- 内容：{result.Content}");
            sb.AppendLine($"- 优先级：{result.Priority}({pri})");
            if (!string.IsNullOrWhiteSpace(result.DueTime)) sb.AppendLine($"- 截止时间：{result.DueTime}");
            if (!string.IsNullOrWhiteSpace(result.ReminderTime)) sb.AppendLine($"- 提醒时间：{result.ReminderTime}");
            if (!string.IsNullOrWhiteSpace(result.Warnings)) sb.AppendLine($"- 说明：{result.Warnings}");
            return AiToolExecResult.Success(sb.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        private async Task<AiToolExecResult> GenerateWeeklyReportAsync(long userId)
        {
            var result = await _sysAi.GenerateWeeklyReportAsync(new SysAiWeeklyReportInput(), userId);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"本周周报已生成（统计周期 {result.PeriodStart} ~ {result.PeriodEnd}，共 {result.Total} 条日程）：");
            if (!string.IsNullOrWhiteSpace(result.Summary)) sb.AppendLine($"\n【整体情况】\n{result.Summary}");
            if (result.Completed.Count > 0) sb.AppendLine($"\n【已完成】\n" + string.Join("\n", result.Completed.Select(x => $"- {x}")));
            if (result.Pending.Count > 0) sb.AppendLine($"\n【未完成/待办】\n" + string.Join("\n", result.Pending.Select(x => $"- {x}")));
            if (result.Risks.Count > 0) sb.AppendLine($"\n【风险提示】\n" + string.Join("\n", result.Risks.Select(x => $"- {x}")));
            return AiToolExecResult.Success(sb.ToString());
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
                var modelImages = AiChatVisionImageCompressor.CompressForModel(
                    AiChatImageHelper.ToModelImageUrls(storeImages, _env?.WebRootPath, options));
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
            var system = text
                .Replace("{{now}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                .Replace("{{userId}}", userId.ToString());

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
        /// 解析回灌历史：最近 <see cref="MaxHistoryMessages"/> 条保留原文，更早的消息用一次 LLM 调用
        /// 聚合成「历史摘要」锚点（结果按窗口边界指纹缓存，避免每轮重算）。摘要失败自动降级为仅窗口，不阻断对话。
        /// </summary>
        /// <returns>(recent 正序消息列表, summaryAnchor 摘要文本，无摘要时为 null)</returns>
        private async Task<(List<AiChatMessage> Recent, string SummaryAnchor)> ResolveHistoryAsync(long sessionId, long userId)
        {
            var history = await Context.Queryable<AiChatMessage>()
                .Where(m => m.SessionId == sessionId && m.UserId == userId && m.Role != "tool")
                .OrderBy(m => m.CreateTime, OrderByType.Desc)
                .Take(MaxHistoryMessages + SummaryMaxOlderMessages)
                .ToListAsync();
            history.Reverse();// 正序

            if (history.Count <= MaxHistoryMessages)
            {
                return (history, null);
            }

            var recent = history.TakeLast(MaxHistoryMessages).ToList();
            var older = history.Take(history.Count - MaxHistoryMessages).ToList();
            var summary = await BuildHistorySummaryAsync(sessionId, userId, older);
            return (recent, summary);
        }

        /// <summary>
        /// 生成（或复用缓存的）历史摘要。缓存指纹取窗口外旧消息的首尾 MessageId，
        /// 窗口边界每约 <see cref="MaxHistoryMessages"/> 轮才移动，期间命中缓存不重复调用模型。
        /// </summary>
        private async Task<string> BuildHistorySummaryAsync(long sessionId, long userId, List<AiChatMessage> older)
        {
            if (older.Count == 0) return null;

            var tenantId = App.GetCurrentTenantId() ?? "0";
            var cacheKey = $"ai:history:summary:{tenantId}:{sessionId}:{older[0].MessageId}-{older[^1].MessageId}";
            var cache = CacheStore.For(CacheBackend.Cache);
            var cached = cache.Get<string>(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                return cached;
            }

            var summaryPrompt = AiHelper.LoadPrompt("system/ai-chat-history-summary.md");
            if (string.IsNullOrWhiteSpace(summaryPrompt))
            {
                // 提示词缺失时静默降级为仅窗口，不影响本轮对话
                _logger.Warn($"AI 历史摘要提示词缺失已跳过摘要：system/ai-chat-history-summary.md sessionId={sessionId}");
                return null;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var m in older)
            {
                sb.AppendLine($"{m.Role}: {AiHelper.ClipText(m.Content, 800)}");
            }

            try
            {
                var options = AiHelper.EnsureAiEnabled();
                var summary = await AiLlmClient.ChatAsync(options, summaryPrompt, sb.ToString(), AiSceneCatalog.AiChat);
                summary = (summary ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    cache.Set(cacheKey, summary, SummaryCacheMinutes);
                    _logger.Info($"AI 历史摘要已生成 sessionId={sessionId} userId={userId} olderCount={older.Count} len={summary.Length}");
                    return summary;
                }
            }
            catch (Exception ex)
            {
                // 摘要失败绝不阻断本轮对话，降级为仅窗口
                _logger.Warn(ex, $"AI 历史摘要生成失败已降级为仅窗口 sessionId={sessionId} userId={userId} olderCount={older.Count}");
            }
            return null;
        }

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
            else if (exec.ChartQuery != null)
            {
                chartQueries.Add(exec.ChartQuery);
            }
            messages.Add(new { role = "tool", tool_call_id = toolCallId, content = AiHelper.ClipText(content, ToolResultMaxLen) });
        }

        /// <summary>
        /// 安全执行工具：捕获异常并记录日志，避免内部异常细节透给前端。
        /// </summary>
        /// <param name="call"></param>
        /// <param name="userId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        private async Task<AiToolExecResult> ExecuteToolSafelyAsync(AiLlmClient.ToolCall call, long userId, long sessionId)
        {
            try
            {
                return await ExecuteToolAsync(call.Name, call.Arguments, userId, sessionId);
            }
            catch (Exception ex)
            {
                // 详情只写后端日志便于排查；回灌给模型的是通用失败提示，
                // 避免把内部异常细节经模型转述暴露给前端。
                _logger.Error($"AI 工具执行异常 tool={call.Name} args={AiHelper.ClipText(call.Arguments ?? "", 300)} err={ex}");
                return AiToolExecResult.Error("该操作执行失败，请告知用户稍后重试");
            }
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
            return text
                .Replace("{{now}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                .Replace("{{userId}}", userId.ToString());
        }

        #endregion helpers
    }
}
