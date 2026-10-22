using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI 调用重试策略接口。
    /// </summary>
    public interface IAiRetryPolicy
    {
        /// <summary>
        /// 执行一次可能需要重试的异步操作。
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="action">要执行的异步操作</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        /// <exception cref="AiException">所有重试均失败时抛出</exception>
        Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            string operationName = "AI call",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 执行一次可能需要重试的异步操作（无返回值）。
        /// </summary>
        Task ExecuteAsync(
            Func<Task> action,
            string operationName = "AI call",
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 指数退避重试策略：适用于网络抖动、临时限流等可恢复错误。
    /// 
    /// 特性：
    /// - 指数退避：延迟 = initialDelayMs * (2 ^ attemptNumber) + 随机抖动
    /// - 自动检测明确不可重试的错误（如 401、403、404）
    /// - 限流(429) 支持：读取 Retry-After 响应头
    /// - 超时递进策略：递进延迟尝试后重新拉长超时
    /// </summary>
    public sealed class ExponentialBackoffRetryPolicy : IAiRetryPolicy
    {
        private readonly ILogger<ExponentialBackoffRetryPolicy> _logger;
        private readonly ExponentialBackoffOptions _options;

        public ExponentialBackoffRetryPolicy(
            ILogger<ExponentialBackoffRetryPolicy> logger,
            ExponentialBackoffOptions options = null)
        {
            _logger = logger;
            _options = options ?? new ExponentialBackoffOptions();
        }

        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            string operationName = "AI call",
            CancellationToken cancellationToken = default)
        {
            var attempt = 0;
            AiException lastException = null;

            while (attempt < _options.MaxRetries)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await action().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw; // 用户取消，不重试
                }
                catch (AiException ex) when (!ShouldRetry(ex))
                {
                    throw; // 明确不可重试的错误，直接抛出
                }
                catch (AiException ex)
                {
                    lastException = ex;
                    attempt++;

                    if (attempt >= _options.MaxRetries)
                    {
                        _logger.LogError(
                            ex,
                            $"AI 操作在经过 {attempt} 次重试后失败 [{operationName}]，最终错误：{ex.ErrorCode}");
                        throw;
                    }

                    // 计算重试延迟
                    var delay = CalculateDelay(ex, attempt);
                    _logger.LogWarning(
                        $"AI 操作失败，{delay}ms 后第 {attempt + 1} 次重试 [{operationName}]，错误：{ex.ErrorCode}，消息：{ex.Message}");

                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // 延迟期间被取消
                    }
                }
                catch (Exception ex)
                {
                    // 非 AI 异常（如网络层异常），有时可尝试转换为 AiHttpException
                    _logger.LogError(ex, $"AI 操作异常（非预期类型）[{operationName}]");
                    throw;
                }
            }

            // 理论上不会到达这里（catch 中已 throw），但为了类型安全
            throw lastException ?? new AiGenericException("unknown", "Unexpected retry exhaustion", "重试失败");
        }

        public async Task ExecuteAsync(
            Func<Task> action,
            string operationName = "AI call",
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(
                async () => { await action().ConfigureAwait(false); return 0; },
                operationName,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 判断是否应该重试该异常。
        /// </summary>
        private static bool ShouldRetry(AiException ex) => ex switch
        {
            // 限流与超时：总是可重试
            AiRateLimitException or AiTimeoutException => true,

            // HTTP 异常：根据状态码判断
            AiHttpException http => http.IsRetryable,

            // 配置异常：永不重试（需要手工修复）
            AiConfigurationException => false,

            // 用户中止：永不重试
            AiCancelledException => false,

            // 其他响应异常：通常可重试（服务端问题）
            AiResponseException => true,

            _ => false
        };

        /// <summary>
        /// 根据异常类型和尝试次数计算重试延迟（毫秒）。
        /// </summary>
        private int CalculateDelay(AiException ex, int attemptNumber)
        {
            // 限流异常：优先使用 Retry-After
            if (ex is AiRateLimitException rateLimitEx && rateLimitEx.RetryAfterSeconds.HasValue)
            {
                var delayMs = rateLimitEx.RetryAfterSeconds.Value * 1000;
                return Math.Min(delayMs, _options.MaxDelayMs);
            }

            // 指数退避 + 抖动（先在 double 中封顶，避免大 attemptNumber 时 int 溢出）
            var exponentialDelayMs = _options.InitialDelayMs * Math.Pow(2, attemptNumber - 1);
            var cappedDelayMs = (int)Math.Min(exponentialDelayMs, _options.MaxDelayMs);

            // 添加随机抖动 ±25% 以避免雷鸣羊群现象
            var jitter = (int)(cappedDelayMs * (Random.Shared.NextDouble() - 0.5) * 0.5);
            return Math.Max(_options.InitialDelayMs, cappedDelayMs + jitter);
        }
    }

    /// <summary>
    /// 指数退避重试的配置选项。
    /// </summary>
    public class ExponentialBackoffOptions
    {
        /// <summary>最大尝试次数（含首次请求），须 &gt;= 1。默认 3，即首次请求加 2 次重试</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>初始重试延迟（毫秒）。默认 500ms</summary>
        public int InitialDelayMs { get; set; } = 500;

        /// <summary>最大重试延迟（毫秒），防止延迟过长。默认 30000ms (30s)</summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// 验证配置的合理性。
        /// </summary>
        public void Validate()
        {
            if (MaxRetries < 1) throw new ArgumentOutOfRangeException(nameof(MaxRetries), "MaxRetries 必须 >= 1");
            if (InitialDelayMs < 100) throw new ArgumentOutOfRangeException(nameof(InitialDelayMs), "InitialDelayMs 必须 >= 100ms");
            if (MaxDelayMs < InitialDelayMs) throw new ArgumentOutOfRangeException(nameof(MaxDelayMs), "MaxDelayMs 必须 >= InitialDelayMs");
        }
    }

    /// <summary>
    /// 无重试策略：调用一次，不重试。用于满足 IAiRetryPolicy 接口但不想启用重试的场景。
    /// </summary>
    public sealed class NoRetryPolicy : IAiRetryPolicy
    {
        private readonly ILogger<NoRetryPolicy> _logger;

        public NoRetryPolicy(ILogger<NoRetryPolicy> logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            string operationName = "AI call",
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (AiException ex)
            {
                _logger.LogError(ex, $"AI 操作失败，不重试 [{operationName}]");
                throw;
            }
        }

        public async Task ExecuteAsync(
            Func<Task> action,
            string operationName = "AI call",
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(
                async () => { await action().ConfigureAwait(false); return 0; },
                operationName,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
