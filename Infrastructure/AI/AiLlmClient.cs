using Infrastructure.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.AI
{
    /// <summary>
    /// 轻量 LLM 客户端：封装 OpenAI 兼容 chat/completions 调用，仅依赖 Infrastructure，
    /// 避免业务层反向引用 ZR.Service。URL 拼装 / 响应读取 / 错误解析逻辑对齐 ZR.Service.AI.AiHelper。
    /// </summary>
    public static class AiLlmClient
    {
        /// <summary>控制台兜底日志出口：宿主未就绪（启动早期/非 Web 宿主工具）时使用。</summary>
        private static readonly ILogger ConsoleLogger =
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Infrastructure.AI.AiLlmClient");

        private static ILogger _hostLogger;

        /// <summary>
        /// 日志出口：优先用宿主 DI 的 ILoggerFactory —— Web 宿主已 <c>builder.Host.UseNLog()</c>，
        /// 因此 ILogger 会经 NLog 落到 logs/all.log 与控制台，无需本工程直接引用 NLog；
        /// 取不到工厂时回退控制台。解析成功即缓存，避免每次调用都查容器。
        /// <para>写法用插值：本类日志一律不传 args，而无 args 时 ILogger 直接输出消息原文，
        /// 因此消息里 JSON 的花括号不会被当成模板占位符（已实测：<c>args={"a":1}</c> 原样输出）。</para>
        /// </summary>
        private static ILogger Logger
        {
            get
            {
                if (_hostLogger != null) return _hostLogger;
                try
                {
                    if (App.ServiceProvider != null)
                    {
                        var factory = App.GetService<ILoggerFactory>();
                        if (factory != null)
                        {
                            _hostLogger = factory.CreateLogger("Infrastructure.AI.AiLlmClient");
                            return _hostLogger;
                        }
                    }
                }
                catch
                {
                    // 宿主未就绪：回退控制台，不因日志影响调用
                }
                return ConsoleLogger;
            }
        }

        public static Uri BuildRequestUri(AiOptions options)
        {
            return BuildRequestUriWith(options, ResolveProvider(options));
        }

        /// <summary>
        /// 基于已解析的 provider 元组构造聊天接口 URI。多模态场景传入已解析的视觉 provider，
        /// 实现文本/视觉模型指向不同端点。
        /// </summary>
        private static Uri BuildRequestUriWith(AiOptions options, (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) resolved)
        {
            var provider = resolved.Provider;
            var baseUrl = (resolved.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var endpoint = (resolved.ChatEndpoint ?? string.Empty).Trim();

            if (!endpoint.StartsWith("/"))
            {
                endpoint = "/" + endpoint;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = GetDefaultBaseUrl(provider);
            }

            return new Uri(new Uri(baseUrl + "/"), endpoint.TrimStart('/'));
        }

        /// <summary>
        /// 发起一次非流式对话，返回模型文本回复。timeout 由 options.TimeoutSeconds 控制。
        /// 优先从 Providers 数组匹配当前 Provider 取配置，空字段回退顶层与硬编码默认值。
        /// </summary>
        public static async Task<string> ChatAsync(AiOptions options, string systemPrompt, string userPrompt, string scene = null)
        {
            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };
            var result = await ChatCoreAsync(options, messages, scene: scene).ConfigureAwait(false);
            return result?.Content ?? string.Empty;
        }

        /// <summary>
        /// 多轮对话重载：直接传入完整 messages 数组（含 system / user / assistant 历史），
        /// 用于校验失败后的纠错重试（self-correction）等需要回灌上下文的场景。
        /// </summary>
        public static async Task<string> ChatWithMessagesAsync(AiOptions options, object[] messages, string scene = null)
        {
            var result = await ChatCoreAsync(options, messages, scene: scene).ConfigureAwait(false);
            return result?.Content ?? string.Empty;
        }

        /// <summary>
        /// ChatAsync / ChatWithMessagesAsync 共用的请求核心：拼装负载、POST、解析 content、记录 token 用量。
        /// 非标准 JSON 响应按纯文本兜底返回。
        /// resolvedOverride 非空时（多模态场景）覆盖 model/baseUrl/endpoint，实现文本与视觉模型解耦。
        /// </summary>
        private static async Task<ChatToolResult> ChatCoreAsync(AiOptions options, object messages,
            (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey)? resolvedOverride = null,
            string scene = null, bool skipThinking = false)
        {
            var resolved = resolvedOverride ?? ResolveProvider(options);
            var uri = BuildRequestUriWith(options, resolved);
            var stopwatch = Stopwatch.StartNew();
            var payload = new Dictionary<string, object>
            {
                ["model"] = resolved.Model,
                ["messages"] = messages,
                ["temperature"] = options.Temperature,
                ["max_tokens"] = options.MaxTokens,
                ["stream"] = false
            };
            ApplyThinkingOptions(payload, resolved.Provider, resolved.Model, options.EnableThinking, skipThinking);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + (resolved.ApiKey ?? string.Empty)
            };

            LogCallStart(scene, resolved.Provider, resolved.Model, false, uri.ToString(), 0, json.Length);
            var lease = await BeginGovernedCallAsync(scene, resolved.Provider, resolved.Model, json, options.MaxTokens, false);
            AiCallOutcome outcome = null;
            try
            {
                var response = await HttpHelper.HttpPostDetailedAsync(
                    uri.ToString(), json, "application/json", ResolveTimeoutSeconds(options, resolved.Provider), headers).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var errDetail = BuildAiErrorMessage(response.StatusCode, response.Content);
                    outcome = Failed("http", "http_error", errDetail, response.StatusCode, response.RequestId);
                    Logger.LogError($"AI 请求失败 scene={SceneName(scene)} status={response.StatusCode} uri={uri} provider={resolved.Provider} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds} body={TruncateForLog(response.Content, 800)}");
                    throw new HttpRequestException(
                        $"AI 服务调用失败（HTTP {response.StatusCode}，model={resolved.Model}）：{TruncateForLog(errDetail, 240)}");
                }
                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    outcome = Failed("empty", "empty_response", "AI 服务返回空响应",
                        response.StatusCode, response.RequestId);
                    LogCallIncomplete(scene, resolved.Provider, resolved.Model, "empty_response", "AI 服务返回空响应", stopwatch.ElapsedMilliseconds);
                    return new ChatToolResult { Content = string.Empty };
                }

                try
                {
                    using var doc = JsonDocument.Parse(response.Content);
                    if (HasProviderError(doc.RootElement))
                    {
                        outcome = Failed("provider", "provider_error",
                            TryReadProviderErrorMessage(response.Content), response.StatusCode, response.RequestId);
                        EnsureNoProviderError(response.Content, doc.RootElement);
                    }
                    var content = ReadContent(doc.RootElement);
                    var usage = ReadUsageTokens(doc.RootElement, resolved.Provider);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        outcome = Failed("empty", "unusable_response", "AI 服务响应无可用内容",
                            response.StatusCode, response.RequestId);
                        outcome.PromptTokens = usage.Prompt;
                        outcome.CompletionTokens = usage.Completion;
                        outcome.TotalTokens = ResolveTotalTokens(usage.Total, usage.Prompt, usage.Completion);
                        outcome.HasUsage = usage.Prompt > 0 || usage.Completion > 0 || usage.Total > 0;
                        LogCallIncomplete(scene, resolved.Provider, resolved.Model, "unusable_response", "AI 服务响应无可用内容", stopwatch.ElapsedMilliseconds);
                        throw new HttpRequestException("AI 服务返回了无法解析的响应，请稍后重试或联系管理员");
                    }
                    outcome = Succeeded(usage, response.StatusCode, response.RequestId);
                    LogCallEnd(scene, resolved.Provider, resolved.Model, false, stopwatch.ElapsedMilliseconds,
                        response.StatusCode, usage.Prompt, usage.Completion,
                        ResolveTotalTokens(usage.Total, usage.Prompt, usage.Completion), content.Length, null, null);
                    LogReasoning(scene, resolved.Model, ReadReasoningContent(doc.RootElement));
                    return ToTextResult(content, usage);
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, $"解析 AI 响应失败，按纯文本处理 scene={SceneName(scene)} model={resolved.Model}");
                    outcome = Succeeded((0, 0, 0), response.StatusCode, response.RequestId, hasUsage: false);
                    return new ChatToolResult { Content = response.Content };
                }
            }
            catch (OperationCanceledException ex)
            {
                if (outcome == null)
                {
                    Logger.LogWarning($"AI 调用超时 scene={SceneName(scene)} provider={resolved.Provider} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds} reason={ex.Message}");
                }
                outcome ??= Failed("timeout", "timeout", ex.Message);
                throw new HttpRequestException("AI 服务响应超时，请稍后重试", ex);
            }
            catch (Exception ex)
            {
                if (outcome == null)
                {
                    Logger.LogError(ex, $"AI 调用异常 scene={SceneName(scene)} provider={resolved.Provider} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds}");
                }
                outcome ??= Failed("failed", ClassifyError(ex), ex.Message);
                throw;
            }
            finally
            {
                await CompleteGovernedCallAsync(lease, outcome);
            }
        }

        /// <summary>
        /// 按当前 Provider 从 Providers 数组匹配，覆盖顶层空字段；未命中则保持顶层值。
        /// 返回解析后用于实际请求的 provider/baseUrl/endpoint/model/apiKey。
        /// 公开供调用方做配置有效性校验，保证校验路径与真实请求路径一致（避免顶层 ApiKey
        /// 为空但 Providers 分项已配置时被误判为未启用）。
        /// </summary>
        public static (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) ResolveProvider(AiOptions options)
        {
            var provider = (options.Provider ?? "openai").Trim().ToLowerInvariant();
            var baseUrl = (options.BaseUrl ?? string.Empty).Trim();
            var endpoint = (options.ChatEndpoint ?? string.Empty).Trim();
            var model = (options.Model ?? string.Empty).Trim();
            var apiKey = (options.ApiKey ?? string.Empty).Trim();

            var matched = (options.Providers ?? new List<AiProviderOptions>())
                .FirstOrDefault(p => string.Equals((p.Provider ?? string.Empty).Trim(), provider, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = (matched.BaseUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(endpoint)) endpoint = (matched.ChatEndpoint ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(model)) model = (matched.Model ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(apiKey)) apiKey = (matched.ApiKey ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = GetDefaultBaseUrl(provider);
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = GetDefaultEndpoint(provider);
            if (string.IsNullOrWhiteSpace(model)) model = GetDefaultModel(provider);
            //Logger.LogInformation($"解析 AI provider：provider={provider}, baseUrl={baseUrl}, endpoint={endpoint}, model={model}");
            return (provider, baseUrl, endpoint, model, apiKey);
        }

        /// <summary>
        /// 千问(DashScope)兼容 OpenAI 协议时 usage 字段名为 input_tokens/output_tokens，
        /// 与 OpenAI 标准的 prompt_tokens/completion_tokens 不同；deepseek 等仍用标准字段。
        /// 目标字段缺失时回退读取另一套字段名；仍缺失按 0。
        /// </summary>
        private static (int Prompt, int Completion, int Total) ReadUsageTokens(JsonElement root, string provider)
        {
            var isQwen = string.Equals((provider ?? "").Trim(), "qwen", StringComparison.OrdinalIgnoreCase);
            var inputKey = isQwen ? "input_tokens" : "prompt_tokens";
            var outputKey = isQwen ? "output_tokens" : "completion_tokens";

            var promptTokens = ReadUsage(root, inputKey);
            var completionTokens = ReadUsage(root, outputKey);

            if (promptTokens == 0) promptTokens = ReadUsage(root, "prompt_tokens");
            if (completionTokens == 0) completionTokens = ReadUsage(root, "completion_tokens");

            var totalTokens = ReadUsage(root, "total_tokens");
            return (promptTokens, completionTokens, totalTokens);
        }

        /// <summary>
        /// 读取 usage 节点下指定 token 计数，缺字段返回 0。
        /// </summary>
        private static int ReadUsage(JsonElement root, string key)
        {
            if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            if (!usage.TryGetProperty(key, out var tokenEl))
            {
                return 0;
            }

            return tokenEl.TryGetInt32(out var value) ? value : 0;
        }

        /// <summary>
        /// 解析视觉模型配置。优先级：
        /// 顶层 Vision* 覆盖 → 目标 Provider 分项（VisionModel / BaseUrl / ApiKey）→ 该 Provider 默认地址。
        /// 目标 Provider = VisionProvider（若填）否则顶层 Provider。
        /// 不会把文本 Model 当成视觉模型，避免把图片发给不支持多模态的模型。
        /// </summary>
        public static (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) ResolveVisionProvider(AiOptions options)
        {
            var provider = (options.VisionProvider ?? options.Provider ?? "openai").Trim().ToLowerInvariant();
            var baseUrl = (options.VisionBaseUrl ?? string.Empty).Trim();
            var endpoint = (options.VisionChatEndpoint ?? string.Empty).Trim();
            var model = (options.VisionModel ?? string.Empty).Trim();
            var apiKey = (options.VisionApiKey ?? options.ApiKey ?? string.Empty).Trim();

            var matched = (options.Providers ?? new List<AiProviderOptions>())
                .FirstOrDefault(p => string.Equals((p.Provider ?? string.Empty).Trim(), provider, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = (matched.BaseUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(endpoint)) endpoint = (matched.ChatEndpoint ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(model)) model = (matched.VisionModel ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(apiKey)) apiKey = (matched.ApiKey ?? string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = GetDefaultBaseUrl(provider);
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = GetDefaultEndpoint(provider);
            Logger.LogInformation($"解析 AI 视觉 provider：provider={provider}, baseUrl={baseUrl}, endpoint={endpoint}, model={model}");
            return (provider, baseUrl, endpoint, model, apiKey);
        }

        /// <summary>
        /// 多模态对话：把文本提示与一组图片 URL 一并发送给支持视觉的模型。
        /// 返回 content 与 usage token；VisionModel 为空时抛友好异常。
        /// </summary>
        public static async Task<ChatToolResult> ChatWithImagesAsync(AiOptions options, string systemPrompt, string textPrompt, List<string> imageUrls, string scene = null)
        {
            var resolved = ResolveVisionProvider(options);
            if (string.IsNullOrWhiteSpace(resolved.Model))
            {
                throw new InvalidOperationException("未配置视觉模型。请在当前 Provider 的 VisionModel（或顶层 AiOptions:VisionModel）中指定支持多模态的模型，如 qwen-vl-plus / gpt-4o-mini。");
            }

            var content = new List<object>
            {
                new { type = "text", text = textPrompt }
            };
            foreach (var url in imageUrls ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                content.Add(new { type = "image_url", image_url = new { url } });
            }

            var messages = new[]
            {
                new { role = "system", content = (object)systemPrompt },
                new { role = "user", content = (object)content }
            };
            return await ChatCoreAsync(options, messages, resolved, scene, skipThinking: true).ConfigureAwait(false);
        }

        /// <summary>
        /// 单个工具调用（tool_calls 元素）。
        /// </summary>
        public class ToolCall
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Arguments { get; set; } // JSON 字符串
        }

        /// <summary>
        /// 单次 chat/completions（tools 模式）的返回：文本内容与待执行的工具调用。
        /// 当 ToolCalls 非空时，上层应执行工具并把结果以 role=tool 回灌，再发起下一轮。
        /// </summary>
        public class ChatToolResult
        {
            /// <summary>模型最终文本（finish 时可能为草稿 JSON 或说明）</summary>
            public string Content { get; set; }

            /// <summary>本轮模型请求执行的工具调用；为空表示模型已直接给出最终结果</summary>
            public List<ToolCall> ToolCalls { get; set; } = new();

            /// <summary>终止原因：stop / tool_calls / length 等</summary>
            public string FinishReason { get; set; }

            /// <summary>本轮调用输入 token（prompt）；usage 缺失时为 0</summary>
            public int PromptTokens { get; set; }

            /// <summary>本轮调用输出 token（completion）；usage 缺失时为 0</summary>
            public int CompletionTokens { get; set; }

            /// <summary>本轮调用合计 token；usage 缺失时为 0</summary>
            public int TotalTokens { get; set; }
        }

        private static ChatToolResult ToTextResult(string content, (int Prompt, int Completion, int Total) usage)
        {
            var total = ResolveTotalTokens(usage.Total, usage.Prompt, usage.Completion);
            return new ChatToolResult
            {
                Content = content ?? string.Empty,
                PromptTokens = usage.Prompt,
                CompletionTokens = usage.Completion,
                TotalTokens = total
            };
        }

        /// <summary>
        /// 带 tools 的多轮对话：支持 OpenAI 兼容 function calling / tool use。
        /// messages 为完整消息数组（调用方持有并维护，含 system/user/assistant/tool）。
        /// tools 为 function 描述数组；模型可请求调用，返回 ToolCalls 后由调用方执行回灌。
        /// 本方法不自动回灌，仅完成单次 HTTP 请求与解析，便于上层控制纠错循环轮次。
        /// </summary>
        public static async Task<ChatToolResult> ChatWithToolsAsync(AiOptions options, object[] messages, object[] tools, string scene = null)
        {
            var resolved = ResolveProvider(options);
            var uri = BuildRequestUri(options);
            var stopwatch = Stopwatch.StartNew();
            var payload = BuildChatPayload(options, resolved, messages, tools, stream: false);
            ApplyThinkingOptions(payload, resolved.Provider, resolved.Model, options.EnableThinking);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + (resolved.ApiKey ?? string.Empty)
            };

            LogCallStart(scene, resolved.Provider, resolved.Model, false, uri.ToString(), tools?.Length ?? 0, json.Length);
            var lease = await BeginGovernedCallAsync(scene, resolved.Provider, resolved.Model, json, options.MaxTokens, false);
            AiCallOutcome outcome = null;
            try
            {
                var response = await HttpHelper.HttpPostDetailedAsync(
                    uri.ToString(), json, "application/json", ResolveTimeoutSeconds(options, resolved.Provider), headers).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var errDetail = BuildAiErrorMessage(response.StatusCode, response.Content);
                    outcome = Failed("http", "http_error", errDetail, response.StatusCode, response.RequestId);
                    Logger.LogError($"AI 请求失败 scene={SceneName(scene)} status={response.StatusCode} uri={uri} provider={resolved.Provider} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds} body={TruncateForLog(response.Content, 800)}");
                    throw new HttpRequestException(
                        $"AI 服务调用失败（HTTP {response.StatusCode}，model={resolved.Model}）：{TruncateForLog(errDetail, 240)}");
                }
                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    outcome = Failed("empty", "empty_response", "AI 服务返回空响应",
                        response.StatusCode, response.RequestId);
                    LogCallIncomplete(scene, resolved.Provider, resolved.Model, "empty_response", "AI 服务返回空响应", stopwatch.ElapsedMilliseconds);
                    throw new HttpRequestException("AI 服务返回空响应，请稍后重试或联系管理员");
                }

                try
                {
                    using var doc = JsonDocument.Parse(response.Content);
                    if (HasProviderError(doc.RootElement))
                    {
                        outcome = Failed("provider", "provider_error",
                            TryReadProviderErrorMessage(response.Content), response.StatusCode, response.RequestId);
                        EnsureNoProviderError(response.Content, doc.RootElement);
                    }
                    var result = ReadToolResult(doc.RootElement);
                    var usage = ReadUsageTokens(doc.RootElement, resolved.Provider);
                    result.PromptTokens = usage.Prompt;
                    result.CompletionTokens = usage.Completion;
                    result.TotalTokens = ResolveTotalTokens(usage.Total, usage.Prompt, usage.Completion);

                    if (string.IsNullOrWhiteSpace(result.Content) && (result.ToolCalls == null || result.ToolCalls.Count == 0))
                    {
                        outcome = Failed("parse", "unusable_response", "AI 服务响应无可用内容",
                            response.StatusCode, response.RequestId);
                        Logger.LogError($"AI 服务响应无可用内容（无 content 且无 tool_calls）。scene={SceneName(scene)} uri={uri} model={resolved.Model} 原始响应={TruncateForLog(response.Content, 600)}");
                        throw new HttpRequestException("AI 服务返回了无法解析的响应，请稍后重试或联系管理员");
                    }
                    outcome = Succeeded(usage, response.StatusCode, response.RequestId);
                    LogCallEnd(scene, resolved.Provider, resolved.Model, false, stopwatch.ElapsedMilliseconds,
                        response.StatusCode, usage.Prompt, usage.Completion, result.TotalTokens,
                        result.Content?.Length ?? 0, result.FinishReason, result.ToolCalls);
                    LogToolCalls(scene, resolved.Model, result.ToolCalls);
                    LogReasoning(scene, resolved.Model, ReadReasoningContent(doc.RootElement));
                    return result;
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, $"解析 AI tool 响应失败，按纯文本处理 scene={SceneName(scene)} model={resolved.Model}");
                    outcome = Succeeded((0, 0, 0), response.StatusCode, response.RequestId, hasUsage: false);
                    return new ChatToolResult { Content = response.Content };
                }
            }
            catch (OperationCanceledException ex)
            {
                if (outcome == null)
                {
                    Logger.LogWarning($"AI 调用超时 scene={SceneName(scene)} provider={resolved.Provider} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds} reason={ex.Message}");
                }
                outcome ??= Failed("timeout", "timeout", ex.Message);
                throw new HttpRequestException("AI 服务响应超时，请稍后重试", ex);
            }
            catch (Exception ex)
            {
                if (outcome == null)
                {
                    Logger.LogError(ex, $"AI 调用异常 scene={SceneName(scene)} provider={resolved.Provider} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds}");
                }
                outcome ??= Failed("failed", ClassifyError(ex), ex.Message);
                throw;
            }
            finally
            {
                await CompleteGovernedCallAsync(lease, outcome);
            }
        }

        /// <summary>
        /// 流式(SSE)返回事件：delta=本轮文本增量（供打字机实时展示）；
        /// finish=本轮结束，Result 为与 ChatWithToolsAsync 同构的聚合结果。
        /// </summary>
        public sealed class AiStreamChunk
        {
            public string Type { get; set; }
            public string Text { get; set; }
            public ChatToolResult Result { get; set; }
        }

        /// <summary>
        /// 流式(SSE)版 ChatWithToolsAsync：OpenAI 兼容 stream=true 的"单轮"对话。
        /// 上层自行控制纠错轮次与工具结果回灌（每轮迭代到 finish 后判断 ToolCalls）。
        /// 迭代中逐块 yield delta；HTTP/解析/服务错误直接抛 HttpRequestException，由上层决定展示策略。
        /// <paramref name="messages"/> 为完整消息数组（含 system/user/assistant/tool），<paramref name="tools"/> 为 function 描述数组。
        /// <paramref name="options"/> 控制模型、温度、token 限制、超时等；<paramref name="scene"/> 用于调用治理。
        /// <paramref name="tools"/>
        /// <paramref name="options"/>
        /// </summary>
        public static async IAsyncEnumerable<AiStreamChunk> StreamChatWithToolsAsync(AiOptions options, object[] messages, object[] tools, string scene = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = ResolveProvider(options);
            var uri = BuildRequestUri(options);
            var stopwatch = Stopwatch.StartNew();
            var payload = BuildChatPayload(options, resolved, messages, tools, stream: true);
            ApplyThinkingOptions(payload, resolved.Provider, resolved.Model, options.EnableThinking);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + (resolved.ApiKey ?? string.Empty)
            };

            LogCallStart(scene, resolved.Provider, resolved.Model, true, uri.ToString(), tools?.Length ?? 0, json.Length);
            var lease = await BeginGovernedCallAsync(scene, resolved.Provider, resolved.Model, json, options.MaxTokens, true);
            AiCallOutcome outcome = null;
            try
            {
                using var streamResp = await HttpHelper.HttpPostReadStreamAsync(uri.ToString(), json, "application/json", ResolveTimeoutSeconds(options, resolved.Provider), headers);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(streamResp.Token, cancellationToken);
                var token = linkedCts.Token;

                var response = streamResp.Response;
                var providerRequestId = HttpHelper.ReadRequestId(response);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(token);
                    outcome = Failed("http", "http_error", TryReadProviderErrorMessage(errBody),
                        (int)response.StatusCode, providerRequestId);
                    Logger.LogError($"AI 流式请求失败 scene={SceneName(scene)} status={(int)response.StatusCode} uri={uri} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds} body={TruncateForLog(errBody, 400)}");
                    throw new HttpRequestException("AI 服务调用失败，请稍后重试或检查 AI 配置");
                }

                var content = new StringBuilder();
                var reasoning = new StringBuilder();
                string finishReason = null;
                var toolAcc = new Dictionary<int, ToolCallBuilder>();
                var promptTokens = 0;
                var completionTokens = 0;
                var totalTokens = 0;

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[8192];
                var lineBytes = new MemoryStream();
                var sseEvents = new List<string>();      // 本条消息内产出的完整 SSE 事件载荷
                var eventData = new StringBuilder();     // 当前事件累积的 data（多行 data: 以 \n 连接）
                var sseDone = false;                     // 是否已收到 [DONE]

                // 行喂入：PushSseLine 可能因超过长度上限抛异常，这里先记治理/审计再抛出，
                // 避免异常只留下 finally 里那句笼统的"未正常完成"（局部函数内无 yield，不违反迭代器限制）
                void FeedSseLine(string line)
                {
                    try
                    {
                        PushSseLine(line, eventData, sseEvents);
                    }
                    catch (HttpRequestException ex)
                    {
                        outcome = Failed("failed", "oversize_response", ex.Message,
                            (int)response.StatusCode, providerRequestId);
                        Logger.LogError(ex, $"AI 流式响应异常，已中断 scene={SceneName(scene)} model={resolved.Model} elapsedMs={stopwatch.ElapsedMilliseconds}");
                        throw;
                    }
                }

                while (!sseDone)
                {
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            LogCallIncomplete(scene, resolved.Provider, resolved.Model, "client_cancelled", "客户端取消了 AI 流式请求", stopwatch.ElapsedMilliseconds);
                            outcome = Failed("cancelled", "client_cancelled", "客户端取消了 AI 流式请求",
                                (int)response.StatusCode, providerRequestId);
                            throw;
                        }
                        LogCallIncomplete(scene, resolved.Provider, resolved.Model, "timeout", "AI 流式响应超时", stopwatch.ElapsedMilliseconds);
                        outcome = Failed("timeout", "timeout", "AI 流式响应超时",
                            (int)response.StatusCode, providerRequestId);
                        throw new HttpRequestException("AI 流式响应超时，连接已中断，请重试");
                    }

                    var reachedEnd = read <= 0;
                    if (reachedEnd)
                    {
                        // 连接结束：末行可能没有换行符（服务端直接断开），尾部事件也可能没有空行收尾，两者都要冲刷，
                        // 否则最后一段增量会丢失。
                        if (lineBytes.Length > 0) FeedSseLine(TakeSseLine(lineBytes));
                        FlushSseEvent(eventData, sseEvents);
                    }
                    else
                    {
                        for (var i = 0; i < read; i++)
                        {
                            if (buffer[i] == (byte)'\n')
                            {
                                FeedSseLine(TakeSseLine(lineBytes));
                                continue;
                            }

                            // 单行长度护栏：异常/恶意响应不得让缓冲无限增长
                            if (lineBytes.Length >= MaxSseLineChars)
                            {
                                outcome = Failed("failed", "oversize_response", "AI 流式响应单行数据超过上限",
                                    (int)response.StatusCode, providerRequestId);
                                Logger.LogError($"AI 流式响应异常：单行数据超过 {MaxSseLineChars} 字符，已中断。scene={SceneName(scene)} model={resolved.Model}");
                                throw new HttpRequestException("AI 流式响应异常，已中断，请重试");
                            }
                            lineBytes.WriteByte(buffer[i]);
                        }
                    }

                    // 统一分发点：逐事件处理用量/终止原因/增量/工具分片（字节循环与流结束冲刷共用同一段逻辑）
                    for (var e = 0; e < sseEvents.Count && !sseDone; e++)
                    {
                        var data = sseEvents[e];
                        if (data == "[DONE]")
                        {
                            sseDone = true;
                            break;
                        }

                        if (data.StartsWith("{") || data.StartsWith("["))
                        {
                            JsonDocument doc;
                            try
                            {
                                doc = JsonDocument.Parse(data);
                            }
                            catch (JsonException)
                            {
                                // 个别不完整块忽略（多行 data: 未收齐时也会走到这里）
                                continue;
                            }
                            using (doc)
                            {
                                var root = doc.RootElement;
                                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out _))
                                {
                                    var msg = TryReadProviderErrorMessage(data);
                                    outcome = Failed("provider", "provider_error", msg,
                                        (int)response.StatusCode, providerRequestId);
                                    Logger.LogError($"AI 流式返回错误 scene={SceneName(scene)} model={resolved.Model} message={msg}");
                                    throw new HttpRequestException("AI 服务调用失败，请稍后重试或检查 AI 配置");
                                }

                                // usage 可能在最后一块与 choices 同场，或独立成块；后到覆盖
                                var (p, c, t) = ReadUsageTokens(root, resolved.Provider);
                                if (p > 0) promptTokens = p;
                                if (c > 0) completionTokens = c;
                                if (t > 0) totalTokens = t;

                                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                                {
                                    continue;
                                }
                                // 约束：仅取 choices[0]。本场景未下发 n 参数，OpenAI 兼容端按单候选返回；
                                // 若将来需要 n>1 的多候选聚合，这里必须改为遍历 choices，否则会静默丢弃候选。
                                var choice = choices[0];

                                if (choice.TryGetProperty("finish_reason", out var frEl) && frEl.ValueKind == JsonValueKind.String)
                                {
                                    var frVal = frEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(frVal)) finishReason = frVal;
                                }

                                if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                                {
                                    continue;
                                }

                                // 思考型模型（Qwen3/DeepSeek-R1 等）的推理增量，仅聚合用于审计日志
                                if (delta.TryGetProperty("reasoning_content", out var rEl) && rEl.ValueKind == JsonValueKind.String)
                                {
                                    var reasoningText = rEl.GetString();
                                    if (!string.IsNullOrEmpty(reasoningText)) reasoning.Append(reasoningText);
                                }

                                if (delta.TryGetProperty("content", out var cEl) && cEl.ValueKind == JsonValueKind.String)
                                {
                                    var txt = cEl.GetString() ?? string.Empty;
                                    if (txt.Length > 0)
                                    {
                                        content.Append(txt);
                                        yield return new AiStreamChunk { Type = "delta", Text = txt };
                                    }
                                }

                                if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var tc in tcs.EnumerateArray())
                                    {
                                        var idx = 0;
                                        if (tc.TryGetProperty("index", out var idxEl) && idxEl.TryGetInt32(out var idxVal)) idx = idxVal;
                                        if (!toolAcc.TryGetValue(idx, out var acc))
                                        {
                                            acc = new ToolCallBuilder();
                                            toolAcc[idx] = acc;
                                        }
                                        if (tc.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                                        {
                                            var idVal = idEl.GetString();
                                            if (!string.IsNullOrWhiteSpace(idVal)) acc.Id = idVal;
                                        }
                                        if (tc.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                                        {
                                            // OpenAI 规范：function.name 仅出现在首个 fragment，且为完整名
                                            if (string.IsNullOrEmpty(acc.Name)
                                                && fn.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String)
                                            {
                                                var nameVal = nEl.GetString();
                                                if (!string.IsNullOrWhiteSpace(nameVal))
                                                {
                                                    acc.Name = nameVal;
                                                    yield return new AiStreamChunk { Type = "tool", Text = nameVal };
                                                }
                                            }
                                            if (fn.TryGetProperty("arguments", out var aEl) && aEl.ValueKind == JsonValueKind.String)
                                            {
                                                acc.Args.Append(aEl.GetString());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            content.Append(data);
                            yield return new AiStreamChunk { Type = "delta", Text = data };
                        }
                    }
                    sseEvents.Clear();

                    if (reachedEnd) break;
                }

                if (!sseDone && string.IsNullOrWhiteSpace(finishReason))
                {
                    outcome = Failed("failed", "stream_interrupted", "AI 流式连接未正常结束",
                        (int)response.StatusCode, providerRequestId);
                    outcome.PromptTokens = promptTokens;
                    outcome.CompletionTokens = completionTokens;
                    outcome.TotalTokens = ResolveTotalTokens(totalTokens, promptTokens, completionTokens);
                    outcome.HasUsage = promptTokens > 0 || completionTokens > 0 || totalTokens > 0;
                    LogCallIncomplete(scene, resolved.Provider, resolved.Model, "stream_interrupted", "AI 流式连接未正常结束", stopwatch.ElapsedMilliseconds);
                    throw new HttpRequestException("AI 流式响应中断，请重试");
                }

                var toolCalls = toolAcc.Values
                    .Where(b => !string.IsNullOrWhiteSpace(b.Name) || b.Args.Length > 0)
                    .Select(b => new ToolCall { Id = b.Id, Name = b.Name, Arguments = b.Args.ToString() })
                    .ToList();

                var resolvedTotalTokens = ResolveTotalTokens(totalTokens, promptTokens, completionTokens);
                var result = new ChatToolResult
                {
                    Content = content.ToString(),
                    ToolCalls = toolCalls,
                    FinishReason = finishReason,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = resolvedTotalTokens
                };

                if (string.IsNullOrWhiteSpace(result.Content) && (result.ToolCalls == null || result.ToolCalls.Count == 0))
                {
                    outcome = Failed("parse", "unusable_response", "AI 服务响应无可用内容",
                        (int)response.StatusCode, providerRequestId);
                    Logger.LogError($"AI 流式响应无可用内容（无 content 且无 tool_calls）。scene={SceneName(scene)} uri={uri} model={resolved.Model}");
                    throw new HttpRequestException("AI 服务返回了无法解析的响应，请稍后重试或联系管理员");
                }
                outcome = Succeeded((promptTokens, completionTokens, resolvedTotalTokens),
                    (int)response.StatusCode, providerRequestId,
                    promptTokens > 0 || completionTokens > 0 || resolvedTotalTokens > 0);
                LogCallEnd(scene, resolved.Provider, resolved.Model, true, stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode, promptTokens, completionTokens, resolvedTotalTokens,
                    content.Length, finishReason, toolCalls);
                LogToolCalls(scene, resolved.Model, toolCalls);
                LogReasoning(scene, resolved.Model, reasoning.ToString());
                yield return new AiStreamChunk { Type = "finish", Result = result };
            }
            finally
            {
                outcome ??= Failed(cancellationToken.IsCancellationRequested ? "cancelled" : "failed",
                    cancellationToken.IsCancellationRequested ? "client_cancelled" : "stream_interrupted",
                    cancellationToken.IsCancellationRequested ? "客户端取消了 AI 流式请求" : "AI 流式请求未正常完成");
                await CompleteGovernedCallAsync(lease, outcome);
            }
        }

        /// <summary>
        /// 流式 tool_calls 分片累积器：OpenAI 兼容 SSE 按 index 分片推送，name/arguments 可能跨块拼接。
        /// </summary>
        private sealed class ToolCallBuilder
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public readonly StringBuilder Args = new StringBuilder();
        }

        /// <summary>SSE 单行长度上限（按字节计，1MB）：正常响应远小于此值；超限即视为异常响应，中断而不是继续吃内存。</summary>
        private const int MaxSseLineChars = 1024 * 1024;

        /// <summary>单个 SSE 事件累积上限（4MB）：多行 <c>data:</c> 拼接后同样必须有上限。</summary>
        private const int MaxSseEventChars = 4 * 1024 * 1024;

        /// <summary>
        /// 取出并清空行缓冲（UTF-8 解码 + 去首尾空白）。按行处理与"流结束时冲刷无换行尾行"共用，
        /// 保证尾行不会因为缺少 '\n' 而被丢弃。
        /// </summary>
        private static string TakeSseLine(MemoryStream lineBytes)
        {
            // 用 ToArray（而非 GetBuffer）取内容：不依赖 MemoryStream 是否为"可公开访问缓冲区"，
            // 单行的这点拷贝相对于随后的字符串分配可以忽略。
            var line = Encoding.UTF8.GetString(lineBytes.ToArray());
            lineBytes.SetLength(0);
            return line.Trim();
        }

        /// <summary>
        /// SSE 单行处理（纯状态机、无 IO，便于单测）：把一行并入当前事件状态。
        /// <para>规范语义：<c>data:</c> 字段累积，多行 <c>data:</c> 以 '\n' 连接；空行表示事件结束；
        /// 以 ':' 开头为注释（心跳），<c>event:</c>/<c>id:</c>/<c>retry:</c> 与内容无关，一律忽略。</para>
        /// <para>兼容性：完整 JSON 或 [DONE] 一出现就立即产出，不依赖厂商是否按规范补空行（否则会退化成"流结束才吐字"）；
        /// 非 JSON 片段（个别厂商直推纯文本）沿用原行为，一行一个事件。</para>
        /// <para>硬约束：单行 / 单事件都有长度上限，超限抛 <see cref="HttpRequestException"/>，防止异常响应把内存撑爆。</para>
        /// </summary>
        /// <param name="line">一行 SSE 文本（不含换行符，行尾 \r 已去除）</param>
        /// <param name="eventData">当前事件的 data 累积缓冲，跨调用保持</param>
        /// <param name="completed">产出的完整事件载荷（追加）</param>
        private static void PushSseLine(string line, StringBuilder eventData, List<string> completed)
        {
            if (line.Length == 0)
            {
                FlushSseEvent(eventData, completed);
                return;
            }

            if (line[0] == ':') return;
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;

            if (line.Length > MaxSseLineChars)
            {
                throw new HttpRequestException($"AI 流式响应单行数据超过上限（{MaxSseLineChars} 字节）");
            }

            var payload = line.Substring(5).Trim();
            if (payload.Length == 0) return;

            if (payload == "[DONE]")
            {
                // 结束标记：先把没发完的事件吐出来，再产出 [DONE]（上层据此结束读取）
                FlushSseEvent(eventData, completed);
                completed.Add("[DONE]");
                return;
            }

            if (payload[0] != '{' && payload[0] != '[')
            {
                // 非 JSON 片段：按原行为立即作为独立事件产出
                FlushSseEvent(eventData, completed);
                completed.Add(payload);
                return;
            }

            if (eventData.Length > 0) eventData.Append('\n');
            eventData.Append(payload);
            if (eventData.Length > MaxSseEventChars)
            {
                throw new HttpRequestException($"AI 流式响应单个事件超过上限（{MaxSseEventChars} 字节）");
            }

            if (LooksCompleteJson(eventData.ToString()))
            {
                completed.Add(eventData.ToString());
                eventData.Clear();
            }
        }

        /// <summary>
        /// 把尚未以空行收尾的事件冲刷为一个完整事件。流结束时必须调用，
        /// 否则"写完事件直接断开连接"的响应会丢掉最后一段增量。
        /// </summary>
        private static void FlushSseEvent(StringBuilder eventData, List<string> completed)
        {
            if (eventData.Length == 0) return;
            completed.Add(eventData.ToString());
            eventData.Clear();
        }

        /// <summary>累积内容是否已是完整 JSON（用于即时产出，不依赖空行分隔）。</summary>
        private static bool LooksCompleteJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text[0] != '{' && text[0] != '[') return false;
            try
            {
                using var doc = JsonDocument.Parse(text);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// 开始治理调用：若 governance 为 null 则返回 null。
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="provider"></param>
        /// <param name="model"></param>
        /// <param name="payload"></param>
        /// <param name="maxTokens"></param>
        /// <param name="isStream"></param>
        /// <returns></returns>
        private static async Task<AiCallLease> BeginGovernedCallAsync(
            string scene, string provider, string model, string payload, int maxTokens, bool isStream)
        {
            var governance = App.GetService<IAiCallGovernance>();
            if (governance == null) return null;
            return await governance.BeginAsync(new AiCallRequest
            {
                Scene = scene,
                Provider = provider,
                Model = model,
                EstimatedPromptTokens = EstimatePromptTokensForGovernance(payload),
                MaxCompletionTokens = Math.Max(0, maxTokens),
                IsStream = isStream
            });
        }

        /// <summary>
        /// 结算治理调用：无论成功/失败/异常，均尝试调用 CompleteAsync 结算；若 governance 为 null 则忽略。
        /// </summary>
        /// <param name="lease"></param>
        /// <param name="outcome"></param>
        /// <returns></returns>
        private static async Task CompleteGovernedCallAsync(AiCallLease lease, AiCallOutcome outcome)
        {
            if (lease == null) return;
            try
            {
                var governance = App.GetService<IAiCallGovernance>();
                if (governance != null)
                {
                    await governance.CompleteAsync(lease, outcome ?? Failed("failed", "unknown", "AI 调用未正常完成"));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, $"AI 调用治理结算失败 requestId={lease.RequestId}");
            }
        }

        /// <summary>
        /// 构建成功的 AiCallOutcome，供 CompleteGovernedCallAsync 结算使用。
        /// </summary>
        /// <param name="usage"></param>
        /// <param name="statusCode"></param>
        /// <param name="requestId"></param>
        /// <param name="hasUsage"></param>
        /// <returns></returns>
        private static AiCallOutcome Succeeded(
            (int Prompt, int Completion, int Total) usage, int? statusCode, string requestId, bool? hasUsage = null) =>
            new()
            {
                Success = true,
                Status = "success",
                HttpStatusCode = statusCode,
                ProviderRequestId = requestId,
                PromptTokens = usage.Prompt,
                CompletionTokens = usage.Completion,
                TotalTokens = ResolveTotalTokens(usage.Total, usage.Prompt, usage.Completion),
                HasUsage = hasUsage ?? (usage.Prompt > 0 || usage.Completion > 0 || usage.Total > 0)
            };

        /// <summary>
        /// 构建失败的 AiCallOutcome，供 CompleteGovernedCallAsync 结算使用。
        /// </summary>
        /// <param name="status"></param>
        /// <param name="errorType"></param>
        /// <param name="message"></param>
        /// <param name="statusCode"></param>
        /// <param name="requestId"></param>
        /// <returns></returns>
        private static AiCallOutcome Failed(
            string status, string errorType, string message, int? statusCode = null, string requestId = null) =>
            new()
            {
                Success = false,
                Status = status,
                ErrorType = errorType,
                ErrorMessage = TruncateForLog(message, 1000),
                HttpStatusCode = statusCode,
                ProviderRequestId = requestId
            };

        private static bool HasProviderError(JsonElement root) =>
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out _);

        private static string ClassifyError(Exception ex) => ex switch
        {
            AiGovernanceDeniedException denied => denied.Reason,
            JsonException => "parse_error",
            HttpRequestException => "http_request",
            _ => "unexpected"
        };

        private static string TruncateForLog(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "…(截断)";
        }

        /// <summary>
        /// 统一的 total token 计算规则：模型未返回 total_tokens 时回退为 prompt + completion。
        /// 普通请求、工具调用、流式请求、审计日志与治理结算必须共用此规则，避免同一笔调用
        /// 出现"日志总数正确、返回给业务层为 0"的口径不一致。
        /// </summary>
        private static int ResolveTotalTokens(int total, int prompt, int completion)
        {
            return total > 0 ? total : prompt + completion;
        }

        /// <summary>场景名兜底，保证审计日志里 scene 字段恒有值。</summary>
        private static string SceneName(string scene) => string.IsNullOrWhiteSpace(scene) ? "default" : scene;

        /// <summary>
        /// 审计：一次模型请求的开始。只记元数据（场景/供应商/模型/流式/工具数/负载规模），
        /// 不落地提示词、图片 Base64 与 ApiKey。
        /// </summary>
        private static void LogCallStart(string scene, string provider, string model, bool isStream, string uri, int toolCount, int payloadChars)
        {
            Logger.LogInformation($"AI 调用开始 scene={SceneName(scene)} provider={provider} model={model} stream={isStream} tools={toolCount} payloadChars={payloadChars} uri={uri}");
        }

        /// <summary>
        /// 审计：一次模型请求成功完成。记录耗时、HTTP 状态、token 用量、内容规模、终止原因与本轮请求的工具名，
        /// 便于核算成本与追溯“模型当时要求调了哪些工具”。
        /// </summary>
        private static void LogCallEnd(string scene, string provider, string model, bool isStream, long elapsedMs,
            int? httpStatus, int promptTokens, int completionTokens, int totalTokens, int contentChars,
            string finishReason, IReadOnlyList<ToolCall> toolCalls)
        {
            var reason = string.IsNullOrWhiteSpace(finishReason) ? "-" : finishReason;
            Logger.LogInformation($"AI 调用完成 scene={SceneName(scene)} provider={provider} model={model} stream={isStream} elapsedMs={elapsedMs} http={httpStatus ?? 0} promptTokens={promptTokens} completionTokens={completionTokens} totalTokens={totalTokens} contentChars={contentChars} finishReason={reason} tools=[{BuildToolSummary(toolCalls)}]");
        }

        /// <summary>
        /// 审计：一次模型请求未正常完成（空响应/流中断等非异常分支），异常分支由调用方自行记录堆栈。
        /// </summary>
        private static void LogCallIncomplete(string scene, string provider, string model, string status, string reason, long elapsedMs)
        {
            Logger.LogWarning($"AI 调用未完成 scene={SceneName(scene)} provider={provider} model={model} status={status} elapsedMs={elapsedMs} reason={TruncateForLog(reason, 300)}");
        }

        /// <summary>审计：逐个记录模型请求的工具调用（名称 + 参数，参数截断避免超长/敏感内容刷屏）。</summary>
        private static void LogToolCalls(string scene, string model, IReadOnlyList<ToolCall> toolCalls)
        {
            if (toolCalls == null || toolCalls.Count == 0) return;
            foreach (var call in toolCalls)
            {
                var toolName = string.IsNullOrWhiteSpace(call.Name) ? "?" : call.Name;
                Logger.LogInformation($"AI 工具调用请求 scene={SceneName(scene)} model={model} tool={toolName} args={TruncateForLog(call.Arguments, 300)}");
            }
        }

        /// <summary>审计：记录思考模型的推理内容（截断），用于确认“思考模式是否真的生效”。</summary>
        private static void LogReasoning(string scene, string model, string reasoning)
        {
            if (string.IsNullOrWhiteSpace(reasoning)) return;
            Logger.LogInformation($"AI 思考内容 scene={SceneName(scene)} model={model} chars={reasoning.Length} text={TruncateForLog(reasoning, 500)}");
        }

        private static string BuildToolSummary(IReadOnlyList<ToolCall> toolCalls)
        {
            if (toolCalls == null || toolCalls.Count == 0) return "-";
            return TruncateForLog(string.Join(", ", toolCalls.Select(c => string.IsNullOrWhiteSpace(c.Name) ? "?" : c.Name)), 300);
        }

        /// <summary>读取 OpenAI 兼容响应中的 message.reasoning_content（思考型模型，如 Qwen3 / DeepSeek-R1）；缺失返回空。</summary>
        private static string ReadReasoningContent(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return message.TryGetProperty("reasoning_content", out var reasoning) && reasoning.ValueKind == JsonValueKind.String
                ? reasoning.GetString()
                : string.Empty;
        }

        /// <summary>
        /// 从 OpenAI 兼容响应解析 tool-use 结果：message.content + message.tool_calls + finish_reason。
        /// </summary>
        private static ChatToolResult ReadToolResult(JsonElement root)
        {
            var result = new ChatToolResult();
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return result;
            }

            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String)
            {
                result.FinishReason = fr.GetString();
            }

            if (!firstChoice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            // 复用公共的 content 读取逻辑，避免与 ReadContent 重复
            result.Content = TryReadMessageContent(message);

            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var call = new ToolCall();
                    if (tc.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        call.Id = id.GetString();
                    }
                    if (tc.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
                    {
                        // 仅记录，OpenAI 当前仅 function
                        _ = type.GetString();
                    }
                    if (tc.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                    {
                        if (fn.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                        {
                            call.Name = name.GetString();
                        }
                        if (fn.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String)
                        {
                            call.Arguments = args.GetString();
                        }
                    }

                    result.ToolCalls.Add(call);
                }
            }

            return result;
        }

        /// <summary>
        /// 从 OpenAI 兼容响应解析 message.content 文本（message.content 为字符串时返回，否则空）。
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public static string ReadContent(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return TryReadMessageContent(message);
        }

        /// <summary>
        /// 从 message 节点读取 content 文本（message.content 为字符串时返回，否则空）。
        /// 同时被 ReadContent 与 ReadToolResult 复用。
        /// </summary>
        private static string TryReadMessageContent(JsonElement message)
        {
            if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// 从 OpenAI 兼容响应解析 usage 节点，返回 prompt/completion/total token 数。
        /// </summary>
        /// <param name="statusCode"></param>
        /// <param name="responseText"></param>
        /// <returns></returns>
        public static string BuildAiErrorMessage(int statusCode, string responseText)
        {
            var detail = TryReadProviderErrorMessage(responseText);
            return string.IsNullOrWhiteSpace(detail)
                ? $"AI request failed({statusCode}): {responseText}"
                : $"AI request failed({statusCode}): {detail}";
        }

        private static string TryReadProviderErrorMessage(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorObj))
                {
                    if (errorObj.ValueKind == JsonValueKind.String)
                    {
                        return errorObj.GetString();
                    }

                    if (errorObj.ValueKind == JsonValueKind.Object && errorObj.TryGetProperty("message", out var msg))
                    {
                        return msg.GetString();
                    }
                }

                if (root.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }
            }
            catch
            {
                // 解析失败时返回原文
            }

            return responseText;
        }

        /// <summary>
        /// 响应若携带 provider 错误（如 OpenAI/千问 error 节点：配额耗尽、鉴权失败等），
        /// 提取 error.message 抛 HttpRequestException，避免被当作空内容静默吞掉难以排查。
        /// 无 error 节点时不做处理。
        /// <paramref name="responseText"/> 原始响应文本，用于日志记录与解析 error.message。
        /// <paramref name="root"/> JsonDocument 根节点，已解析的响应 JSON。
        /// </summary>
        private static void EnsureNoProviderError(string responseText, JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out _))
            {
                return;
            }

            var message = TryReadProviderErrorMessage(responseText);
            Logger.LogError($"AI 服务返回错误响应：{message}");
            throw new HttpRequestException("AI 服务调用失败，请稍后重试或检查 AI 配置");
        }

        /// <summary>
        /// 按 provider/model 判断是否写入 enable_thinking，并记录审计日志（思考模式是否生效、未生效的原因）。
        /// 仅通义文本混合思考模型需要；看图请求或视觉模型（qwen-vl-* 等）带此字段常直接 400。
        /// 返回是否实际写入了 enable_thinking。
        /// </summary>
        private static bool ApplyThinkingOptions(Dictionary<string, object> payload, string provider, string model, bool enableThinking, bool skipThinking = false)
        {
            if (skipThinking)
            {
                Logger.LogDebug($"AI 思考模式未启用 provider={provider} model={model} 原因=看图/多模态请求不写 enable_thinking");
                return false;
            }
            if (!string.Equals((provider ?? "").Trim(), "qwen", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (IsVisionModel(model))
            {
                Logger.LogDebug($"AI 思考模式未启用 provider={provider} model={model} 原因=视觉模型不支持 enable_thinking");
                return false;
            }
            payload["enable_thinking"] = enableThinking;
            //Logger.LogInformation($"AI 思考模式设置 enable_thinking={enableThinking} provider={provider} model={model}");
            return true;
        }

        /// <summary>名称含 vl / vision 的视为视觉模型，不写 enable_thinking。</summary>
        private static bool IsVisionModel(string model)
        {
            var m = (model ?? "").Trim().ToLowerInvariant();
            return m.Contains("-vl") || m.Contains("vl-") || m.Contains("vision");
        }

        /// <summary>
        /// 组装 chat/completions 负载。Ollama 等本地兼容端对 OpenAI 扩展字段（stream_options）支持差，
        /// 多带会导致挂起或空响应；tools 为空时也不下发 tool_choice。
        /// </summary>
        private static Dictionary<string, object> BuildChatPayload(
            AiOptions options,
            (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) resolved,
            object[] messages,
            object[] tools,
            bool stream)
        {
            var payload = new Dictionary<string, object>
            {
                ["model"] = resolved.Model,
                ["messages"] = messages,
                ["temperature"] = options.Temperature,
                ["max_tokens"] = options.MaxTokens,
                ["stream"] = stream
            };

            if (tools != null && tools.Length > 0)
            {
                payload["tools"] = tools;
                payload["tool_choice"] = "auto";
            }

            // stream_options.include_usage 仅 OpenAI 系可靠；Ollama 常不认导致长时间无分片
            if (stream && SupportsStreamUsageOptions(resolved.Provider))
            {
                payload["stream_options"] = new Dictionary<string, object> { ["include_usage"] = true };
            }

            return payload;
        }

        private static bool SupportsStreamUsageOptions(string provider)
        {
            var p = (provider ?? "").Trim().ToLowerInvariant();
            return p is "openai" or "deepseek" or "qwen" or "bigmodel";
        }

        private static string GetDefaultBaseUrl(string provider)
        {
            return (provider ?? "openai").Trim().ToLowerInvariant() switch
            {
                "deepseek" => "https://api.deepseek.com",
                "qwen" => "https://dashscope.aliyuncs.com/compatible-mode/v1",
                "ollama" => "http://127.0.0.1:11434/v1",
                _ => "https://api.openai.com"
            };
        }

        private static string GetDefaultEndpoint(string provider)
        {
            return (provider ?? "openai").Trim().ToLowerInvariant() switch
            {
                "deepseek" => "/chat/completions",
                "qwen" => "/chat/completions",
                "ollama" => "/chat/completions",
                _ => "/v1/chat/completions"
            };
        }

        private static string GetDefaultModel(string provider)
        {
            return (provider ?? "openai").Trim().ToLowerInvariant() switch
            {
                "deepseek" => "deepseek-chat",
                "qwen" => "qwen-turbo",
                "ollama" => "qwen2.5:7b",
                _ => "gpt-4o-mini"
            };
        }

        /// <summary>Ollama 等本地服务不强制云厂商 ApiKey。</summary>
        public static bool AllowsEmptyApiKey(string provider)
        {
            return string.Equals((provider ?? "").Trim(), "ollama", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 治理预估 token：含 data URI 时按文件体积估算，避免把 Base64 字符数当成 prompt tokens 撑爆额度。
        /// </summary>
        private static int EstimatePromptTokensForGovernance(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return 0;
            // data:image/...;base64,XXXX —— Base64 约 4/3 原文件，视觉模型按图块计费，给固定上限即可
            const string marker = "data:image/";
            var idx = payload.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return Math.Max(1, Math.Min(payload.Length / 4, 200_000));
            }
            var textLen = idx;
            var dataUriApproxBytes = Math.Max(0, (payload.Length - idx) * 3 / 4);
            var imageTokens = Math.Min(8_000, Math.Max(1_000, dataUriApproxBytes / 256));
            var textTokens = Math.Max(1, textLen / 4);
            return Math.Min(textTokens + imageTokens, 50_000);
        }

        /// <summary>分项 TimeoutSeconds 优先，否则用顶层，缺省 60 秒。</summary>
        public static int ResolveTimeoutSeconds(AiOptions options, string provider = null)
        {
            var p = (provider ?? options?.Provider ?? "").Trim();
            var matched = (options?.Providers ?? new List<AiProviderOptions>())
                .FirstOrDefault(x => string.Equals((x.Provider ?? string.Empty).Trim(), p, StringComparison.OrdinalIgnoreCase));
            if (matched?.TimeoutSeconds is int t && t > 0) return t;
            var top = options?.TimeoutSeconds ?? 0;
            return top > 0 ? top : 60;
        }
    }
}
