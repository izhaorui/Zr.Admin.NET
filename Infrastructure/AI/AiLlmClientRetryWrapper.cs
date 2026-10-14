using Infrastructure.Model;
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
    /// - 将底层 HttpRequestException 等转换为标准的 AiException 体系
    /// - 上层调用者可统一捕获 AiException 进行错误分类与国际化提示
    /// - 保持与原 AiLlmClient 的 API 兼容性
    /// </summary>
    public static class AiLlmClientRetryWrapper
    {
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
        /// 非流式对话（自动重试）。
        /// </summary>
        public static async Task<string> ChatAsyncWithRetry(
            AiOptions options,
            string systemPrompt,
            string userPrompt,
            string scene = null)
        {
            var retryPolicy = BuildRetryPolicy(options);
            var result = await retryPolicy.ExecuteAsync(
                async () =>
                {
                    try
                    {
                        return await AiLlmClient.ChatAsync(options, systemPrompt, userPrompt, scene)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw ConvertException(ex);
                    }
                },
                operationName: $"Chat (scene={scene}, model={options?.Model})")
                .ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// 多轮对话（自动重试）。
        /// </summary>
        public static async Task<string> ChatWithMessagesAsyncWithRetry(
            AiOptions options,
            object[] messages,
            string scene = null)
        {
            var retryPolicy = BuildRetryPolicy(options);
            var result = await retryPolicy.ExecuteAsync(
                async () =>
                {
                    try
                    {
                        return await AiLlmClient.ChatWithMessagesAsync(options, messages, scene)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw ConvertException(ex);
                    }
                },
                operationName: $"ChatWithMessages (scene={scene}, model={options?.Model})")
                .ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// 多模态对话（自动重试）。
        /// </summary>
        public static async Task<AiLlmClient.ChatToolResult> ChatWithImagesAsyncWithRetry(
            AiOptions options,
            string systemPrompt,
            string textPrompt,
            List<string> imageUrls,
            string scene = null)
        {
            var retryPolicy = BuildRetryPolicy(options);
            var result = await retryPolicy.ExecuteAsync(
                async () =>
                {
                    try
                    {
                        return await AiLlmClient.ChatWithImagesAsync(options, systemPrompt, textPrompt, imageUrls, scene)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw ConvertException(ex);
                    }
                },
                operationName: $"ChatWithImages (scene={scene}, model={options?.Model}, images={imageUrls?.Count ?? 0})")
                .ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Function Calling（非流式，自动重试）。
        /// </summary>
        public static async Task<AiLlmClient.ChatToolResult> ChatWithToolsAsyncWithRetry(
            AiOptions options,
            object[] messages,
            object[] tools,
            string scene = null)
        {
            var retryPolicy = BuildRetryPolicy(options);
            var result = await retryPolicy.ExecuteAsync(
                async () =>
                {
                    try
                    {
                        return await AiLlmClient.ChatWithToolsAsync(options, messages, tools, scene)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw ConvertException(ex);
                    }
                },
                operationName: $"ChatWithTools (scene={scene}, model={options?.Model}, tools={tools?.Length ?? 0})")
                .ConfigureAwait(false);

            return result;
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

            // 通用 HTTP 错误
            return new AiHttpException(
                null, "http_error",
                ex.Message ?? "HTTP 请求失败",
                "AI 服务调用失败，请稍后重试");
        }
    }
}
