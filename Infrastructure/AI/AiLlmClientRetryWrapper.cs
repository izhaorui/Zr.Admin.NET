using Infrastructure.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI LLM 客户端重试包装器：在 AiLlmClient 基础上添加自动重试与异常标准化。
    /// 
    /// 设计目标：
    /// - 对网络抖动、限流(429)、临时服务不可用(503)等可恢复错误自动重试
    /// - 同一厂商配了多条渠道（生产 Key / 备用 Key）时，当前渠道重试耗尽后换下一条再试
    /// - 将底层 HttpRequestException 等转换为标准的 AiException 体系
    /// - 上层调用者可统一捕获 AiException 进行错误分类与国际化提示
    /// - 保持与原 AiLlmClient 的 API 兼容性
    /// </summary>
    public static class AiLlmClientRetryWrapper
    {
        /// <summary>配置诊断日志出口（与 AiLlmClient 同一套：宿主优先、控制台兜底）。</summary>
        private static ILogger Logger => AiLog.GetLogger("Infrastructure.AI.AiLlmClientRetryWrapper");
        /// <summary>
        /// 构建重试策略（基于 AiOptions 配置）。
        /// </summary>
        private static IAiRetryPolicy BuildRetryPolicy(AiOptions options)
        {
            if (options == null || !options.EnableRetry || options.MaxRetries <= 0)
            {
                return new NoRetryPolicy(AiLog.GetLogger<NoRetryPolicy>());
            }

            var retryOptions = new ExponentialBackoffOptions
            {
                MaxRetries = options.MaxRetries,
                InitialDelayMs = Math.Max(100, options.InitialRetryDelayMs),
                MaxDelayMs = Math.Max(1000, options.MaxRetryDelayMs)
            };

            retryOptions.Validate();

            return new ExponentialBackoffRetryPolicy(
                AiLog.GetLogger<ExponentialBackoffRetryPolicy>(),
                retryOptions);
        }

        /// <summary>
        /// 非流式对话（自动重试；失败时按渠道候选链换 Key 重试）。
        /// </summary>
        public static Task<string> ChatAsyncWithRetry(
            AiOptions options,
            string systemPrompt,
            string userPrompt,
            string scene = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteWithFailoverAsync(
                options,
                scene,
                $"Chat (scene={scene})",
                config => AiLlmClient.ChatAsync(config, systemPrompt, userPrompt, scene, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// 多轮对话（自动重试；失败时按渠道候选链换 Key 重试）。
        /// </summary>
        public static Task<string> ChatWithMessagesAsyncWithRetry(
            AiOptions options,
            object[] messages,
            string scene = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteWithFailoverAsync(
                options,
                scene,
                $"ChatWithMessages (scene={scene})",
                config => AiLlmClient.ChatWithMessagesAsync(config, messages, scene, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// 多模态对话（自动重试）。
        /// <para>不参与渠道换 Key：视觉模型与其密钥是独立配置（VisionProvider/VisionApiKey），
        /// 用文本渠道的备用 Key 顶替没有意义，只做同渠道重试。</para>
        /// </summary>
        public static Task<AiLlmClient.ChatToolResult> ChatWithImagesAsyncWithRetry(
            AiOptions options,
            string systemPrompt,
            string textPrompt,
            List<string> imageUrls,
            string scene = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteOnceAsync(
                options,
                $"ChatWithImages (scene={scene}, model={options?.Model}, images={imageUrls?.Count ?? 0})",
                config => AiLlmClient.ChatWithImagesAsync(config, systemPrompt, textPrompt, imageUrls, scene, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// Function Calling（非流式，自动重试；失败时按渠道候选链换 Key 重试）。
        /// </summary>
        public static Task<AiLlmClient.ChatToolResult> ChatWithToolsAsyncWithRetry(
            AiOptions options,
            object[] messages,
            object[] tools,
            string scene = null,
            CancellationToken cancellationToken = default)
        {
            return ExecuteWithFailoverAsync(
                options,
                scene,
                $"ChatWithTools (scene={scene}, tools={tools?.Length ?? 0})",
                config => AiLlmClient.ChatWithToolsAsync(config, messages, tools, scene, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// 单渠道执行：构建重试策略、调用、把底层异常标准化为 <see cref="AiException"/>。
        /// </summary>
        private static async Task<T> ExecuteOnceAsync<T>(
            AiOptions options,
            string operationName,
            Func<AiOptions, Task<T>> call,
            CancellationToken cancellationToken)
        {
            var retryPolicy = BuildRetryPolicy(options);
            return await retryPolicy.ExecuteAsync(
                async () =>
                {
                    try
                    {
                        return await call(options).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw ConvertException(ex);
                    }
                },
                operationName: $"{operationName}, model={options?.Model}",
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 按渠道候选链执行：先试当前生效渠道（同渠道内按策略重试），
        /// 遇到“换 Key 有意义”的错误（鉴权失败/限流/超时/5xx）再换下一条同厂商渠道。
        /// <para>
        /// 只有一条候选（文件配置模式、库中单行）时等价于原来的单渠道重试，行为不变；
        /// 用户取消（<see cref="OperationCanceledException"/>/<see cref="AiCancelledException"/>）
        /// 与环境无关的配置错误不触发换渠道，避免把用户的取消拖成多次请求。
        /// </para>
        /// </summary>
        private static async Task<T> ExecuteWithFailoverAsync<T>(
            AiOptions options,
            string scene,
            string operationName,
            Func<AiOptions, Task<T>> call,
            CancellationToken cancellationToken)
        {
            var chain = AiLlmClient.ResolveProviderChain(options);
            Exception lastError = null;

            for (var index = 0; index < chain.Count; index++)
            {
                var candidate = chain[index];
                var candidateOptions = WithCandidate(options, candidate);

                try
                {
                    return await ExecuteOnceAsync(
                        candidateOptions,
                        $"{operationName}, provider={candidate.Provider}, 候选={index + 1}/{chain.Count}",
                        call,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (index < chain.Count - 1 && ShouldFailover(ex, cancellationToken))
                {
                    lastError = ex;
                    Logger.LogWarning($"AI 渠道失败，切换备用渠道 候选={index + 1}/{chain.Count} provider={candidate.Provider} model={candidate.Model} 错误={ex.GetType().Name}:{ex.Message}");
                }
            }

            throw lastError ?? new AiGenericException("no_available_channel", $"AI 渠道均不可用（scene={scene}）");
        }

        /// <summary>按候选渠道构造临时配置：只接管顶层文本字段，其它全局参数（温度/超时/重试）原样沿用。</summary>
        private static AiOptions WithCandidate(AiOptions options, (string Provider, string BaseUrl, string ChatEndpoint, string Model, string ApiKey) candidate)
        {
            var copy = options.CloneShallow();
            copy.Provider = candidate.Provider;
            copy.BaseUrl = candidate.BaseUrl;
            copy.ChatEndpoint = candidate.ChatEndpoint;
            copy.Model = candidate.Model;
            copy.ApiKey = candidate.ApiKey;
            return copy;
        }

        /// <summary>
        /// 判断该错误是否值得换一条渠道重试：换 Key 能解决的（鉴权、限流、超时、服务端错误）才换；
        /// 用户取消与配置错误换渠道也没用，直接抛出。
        /// </summary>
        private static bool ShouldFailover(Exception ex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            return ex switch
            {
                AiCancelledException => false,
                AiConfigurationException => false,
                AiRateLimitException => true,
                AiTimeoutException => true,
                AiResponseException => true,
                AiHttpException http => http.HttpStatusCode is null
                                         || http.HttpStatusCode == 401
                                         || http.HttpStatusCode == 403
                                         || http.HttpStatusCode == 404
                                         || http.HttpStatusCode == 408
                                         || http.HttpStatusCode == 429
                                         || http.HttpStatusCode >= 500,
                _ => false
            };
        }

        /// <summary>
        /// Function Calling（流式，异常标准化）。
        /// <para>不复用非流式那套自动重试：流一旦开始下发 chunk，中途重试会造成消息重复/错乱，
        /// 故本方法<strong>不做重试</strong>，只把底层异常标准化为 <see cref="AiException"/>（取消异常除外，见
        /// <see cref="ConvertStreamException"/>）。方法名刻意不带 <c>WithRetry</c>，避免调用方误解为重试调用。</para>
        /// </summary>
        public static async IAsyncEnumerable<AiLlmClient.AiStreamChunk> StreamChatWithToolsAsync(
            AiOptions options,
            object[] messages,
            object[] tools,
            string scene = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 迭代段内不得被 try/catch 包裹（含 yield return），否则编译器报错；
            // 这里手动驱动内部枚举器，仅在其 MoveNextAsync 外层捕获并转换异常。
            await using var enumerator = AiLlmClient
                .StreamChatWithToolsAsync(options, messages, tools, scene, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            while (true)
            {
                AiLlmClient.AiStreamChunk chunk;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) break;
                    chunk = enumerator.Current;
                }
                catch (Exception ex)
                {
                    throw ConvertStreamException(ex);
                }

                yield return chunk;
            }
        }


        /// <summary>
        /// 流式场景异常标准化：与非流式一致把底层异常转成 <see cref="AiException"/>，
        /// 但<strong>保留取消语义</strong>——上游 SSE 控制器依赖 <see cref="OperationCanceledException"/>
        /// 区分“客户端断开/请求取消”（静默收尾）与真实错误（下发 error 事件），
        /// 若转成 <see cref="AiCancelledException"/> 会破坏该判断并产生误报日志。
        /// </summary>
        private static Exception ConvertStreamException(Exception ex)
        {
            if (ex is OperationCanceledException || ex is AiException) return ex;
            return ConvertException(ex);
        }

        /// <summary>
        /// 将底层异常转换为 AiException 体系（用于包装的调用点）。
        /// </summary>
        private static AiException ConvertException(Exception ex)
        {
            return ex switch
            {
                AiException ai => ai, // 已是标准异常

                // 治理拒绝（额度不足/功能停用/未配单价等）自带面向用户的中文原因，必须原样透出。
                // 若落到下面的兜底分支，UserMessage 会被替换成"AI 服务异常，请稍后重试或联系管理员"，
                // 前端只能看到通用文案，用户无法得知是被额度或开关拦下的。
                AiGovernanceDeniedException denied
                    => new AiGenericException(denied.Reason, denied.Message, denied.Message),

                OperationCanceledException => new AiCancelledException("operation_cancelled", null),

                HttpRequestException httpEx => TryParseHttpException(httpEx),

                _ => new AiGenericException("unexpected_error", ex.Message)
            };
        }

        /// <summary>
        /// 尝试从 HttpRequestException 中提取 HTTP 状态码（如果有）。
        /// </summary>
        private static AiHttpException TryParseHttpException(HttpRequestException ex)
        {
            // 检查是否是由于超时
            if (ex.InnerException is OperationCanceledException)
            {
                return new AiHttpException(
                    null, "timeout",
                    "AI 服务响应超时",
                    "请求超时，请稍后重试");
            }

            var statusCode = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : (int?)null;
            return new AiHttpException(
                statusCode,
                "http_error",
                ex.Message ?? "HTTP 请求失败",
                BuildUserMessage(statusCode));
        }

        /// <summary>
        /// 根据 HTTP 状态码生成可展示给用户的处理建议（不含敏感信息）。
        /// </summary>
        private static string BuildUserMessage(int? statusCode)
        {
            return statusCode switch
            {
                401 or 403 => $"AI 服务鉴权失败（HTTP {statusCode}），请检查 API Key 配置",
                404 => $"AI 接口地址或模型不存在（HTTP {statusCode}），请检查 BaseUrl/ChatEndpoint/Model",
                429 => "AI 服务请求过于频繁（HTTP 429），请稍后重试",
                >= 500 => $"AI 服务暂不可用（HTTP {statusCode}），请稍后重试",
                _ => "AI 服务调用失败，请稍后重试"
            };
        }
    }
}
