using Infrastructure.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Helper
{
    /// <summary>
    /// 轻量 LLM 客户端：封装 OpenAI 兼容 chat/completions 调用，仅依赖 Infrastructure，
    /// 避免业务层反向引用 ZR.Service。URL 拼装 / 响应读取 / 错误解析逻辑对齐 ZR.Service.AI.AiHelper。
    /// </summary>
    public static class AiLlmClient
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Infrastructure.Helper.AiLlmClient");

        public static Uri BuildRequestUri(AiOptions options)
        {
            var resolved = ResolveProvider(options);
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
        public static async Task<string> ChatAsync(AiOptions options, string systemPrompt, string userPrompt)
        {
            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };
            return await ChatCoreAsync(options, messages).ConfigureAwait(false);
        }

        /// <summary>
        /// 多轮对话重载：直接传入完整 messages 数组（含 system / user / assistant 历史），
        /// 用于校验失败后的纠错重试（self-correction）等需要回灌上下文的场景。
        /// </summary>
        public static async Task<string> ChatWithMessagesAsync(AiOptions options, object[] messages)
        {
            return await ChatCoreAsync(options, messages).ConfigureAwait(false);
        }

        /// <summary>
        /// ChatAsync / ChatWithMessagesAsync 共用的请求核心：拼装负载、POST、解析 content、记录 token 用量。
        /// 非标准 JSON 响应按纯文本兜底返回。
        /// </summary>
        private static async Task<string> ChatCoreAsync(AiOptions options, object messages)
        {
            var resolved = ResolveProvider(options);
            var uri = BuildRequestUri(options);
            var payload = new Dictionary<string, object>
            {
                ["model"] = resolved.Model,
                ["messages"] = messages,
                ["temperature"] = options.Temperature,
                ["max_tokens"] = options.MaxTokens,
                ["stream"] = false
            };
            ApplyThinkingOptions(payload, resolved.Provider, options.EnableThinking);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + (resolved.ApiKey ?? string.Empty)
            };

            var responseText = await HttpHelper.HttpPostAsync(
                uri.ToString(),
                json,
                "application/json",
                options.TimeoutSeconds,
                headers).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return string.Empty;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                EnsureNoProviderError(responseText, doc.RootElement);
                var content = ReadContent(doc.RootElement);
                LogUsage(doc.RootElement, resolved.Provider, resolved.Model);
                return content;
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "解析 AI 响应失败，按纯文本处理");
                // 非标准 JSON（如直接返回纯文本）时原样返回，由上层剥离/解析
                return responseText;
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
            Logger.LogInformation("解析 AI provider: {Provider}, baseUrl={BaseUrl}, endpoint={Endpoint}, model={Model}", provider, baseUrl, endpoint, model);
            return (provider, baseUrl, endpoint, model, apiKey);
        }

        /// <summary>
        /// 从 OpenAI 兼容响应读取 usage 并日志打印 token 输入/输出/合计。
        /// 部分 Provider 可能不返回 usage，缺字段时按 0 打印，不抛异常。
        /// </summary>
        private static void LogUsage(JsonElement root, string provider, string model)
        {
            try
            {
                // 千问(DashScope)兼容 OpenAI 协议时 usage 字段名为 input_tokens/output_tokens，
                // 与 OpenAI 标准的 prompt_tokens/completion_tokens 不同；deepseek 等仍用标准字段。
                var isQwen = string.Equals((provider ?? "").Trim(), "qwen", StringComparison.OrdinalIgnoreCase);
                var inputKey = isQwen ? "input_tokens" : "prompt_tokens";
                var outputKey = isQwen ? "output_tokens" : "completion_tokens";

                var promptTokens = ReadUsage(root, inputKey);
                var completionTokens = ReadUsage(root, outputKey);

                // 若目标字段缺失（如千问网关同时返回两套字段名），回退读取另一套字段名
                if (promptTokens == 0) promptTokens = ReadUsage(root, "prompt_tokens");
                if (completionTokens == 0) completionTokens = ReadUsage(root, "completion_tokens");

                var totalTokens = ReadUsage(root, "total_tokens");
                Logger.LogInformation("AI token usage [provider={Provider}, model={Model}] 输入(prompt)={PromptTokens} 输出(completion)={CompletionTokens} 合计(total)={TotalTokens}",
                    provider, model, promptTokens, completionTokens, totalTokens);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "读取 AI token 用量失败");
            }
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
        }

        /// <summary>
        /// 带 tools 的多轮对话：支持 OpenAI 兼容 function calling / tool use。
        /// messages 为完整消息数组（调用方持有并维护，含 system/user/assistant/tool）。
        /// tools 为 function 描述数组；模型可请求调用，返回 ToolCalls 后由调用方执行回灌。
        /// 本方法不自动回灌，仅完成单次 HTTP 请求与解析，便于上层控制纠错循环轮次。
        /// </summary>
        public static async Task<ChatToolResult> ChatWithToolsAsync(AiOptions options, object[] messages, object[] tools)
        {
            var resolved = ResolveProvider(options);
            var uri = BuildRequestUri(options);
            var payload = new Dictionary<string, object>
            {
                ["model"] = resolved.Model,
                ["messages"] = messages,
                ["tools"] = tools,
                ["tool_choice"] = "auto",
                ["temperature"] = options.Temperature,
                ["max_tokens"] = options.MaxTokens,
                ["stream"] = false
            };
            ApplyThinkingOptions(payload, resolved.Provider, options.EnableThinking);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + (resolved.ApiKey ?? string.Empty)
            };

            var responseText = await HttpHelper.HttpPostAsync(
                uri.ToString(),
                json,
                "application/json",
                options.TimeoutSeconds,
                headers).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return new ChatToolResult();
            }

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                EnsureNoProviderError(responseText, doc.RootElement);
                var result = ReadToolResult(doc.RootElement);
                LogUsage(doc.RootElement, resolved.Provider, resolved.Model);
                return result;
            }
            catch (JsonException ex)
            {
                // 非标准响应：原样作为文本兜底
                Logger.LogWarning(ex, "解析 AI tool 响应失败，按纯文本处理");
                return new ChatToolResult { Content = responseText };
            }
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
        /// </summary>
        private static void EnsureNoProviderError(string responseText, JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out _))
            {
                return;
            }

            var message = TryReadProviderErrorMessage(responseText);
            Logger.LogError("AI 服务返回错误响应：{Message}", message);
            throw new HttpRequestException(string.IsNullOrWhiteSpace(message) ? "AI 服务返回错误响应" : message);
        }

        /// <summary>
        /// 按 provider 判断是否支持 enable_thinking（混合思考模型：Qwen3 系列等），
        /// 支持则在请求体显式写入该参数，避免默认开启思考导致首字延迟高。其余 provider 不加。
        /// </summary>
        private static void ApplyThinkingOptions(Dictionary<string, object> payload, string provider, bool enableThinking)
        {
            var isQwen = string.Equals((provider ?? "").Trim(), "qwen", StringComparison.OrdinalIgnoreCase);
            if (isQwen)
            {
                payload["enable_thinking"] = enableThinking;
            }
        }

        private static string GetDefaultBaseUrl(string provider)
        {
            return (provider ?? "openai").Trim().ToLowerInvariant() switch
            {
                "deepseek" => "https://api.deepseek.com",
                "qwen" => "https://dashscope.aliyuncs.com/compatible-mode/v1",
                _ => "https://api.openai.com"
            };
        }

        private static string GetDefaultEndpoint(string provider)
        {
            return (provider ?? "openai").Trim().ToLowerInvariant() switch
            {
                "deepseek" => "/chat/completions",
                "qwen" => "/chat/completions",
                _ => "/v1/chat/completions"
            };
        }

        private static string GetDefaultModel(string provider)
        {
            return (provider ?? "openai").Trim().ToLowerInvariant() switch
            {
                "deepseek" => "deepseek-chat",
                "qwen" => "qwen-turbo",
                _ => "gpt-4o-mini"
            };
        }
    }
}
