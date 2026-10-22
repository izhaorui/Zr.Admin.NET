using System;
using System.Net;

namespace Infrastructure.AI
{
    /// <summary>
    /// 通用 AI 异常：当具体类型无法确定时使用。
    /// </summary>
    public sealed class AiGenericException : AiException
    {
        public AiGenericException(string errorCode, string message, string userMessage = null)
            : base("generic", errorCode, message, userMessage ?? "AI 服务异常，请稍后重试或联系管理员")
        {
        }
    }

    /// <summary>
    /// AI 服务调用的基础异常类。所有 AI 相关异常均继承自此类，便于上层统一捕获与分类。
    /// </summary>
    public abstract class AiException : Exception
    {
        /// <summary>错误分类：用于日志统计与埋点</summary>
        public string ErrorCategory { get; protected set; }

        /// <summary>错误代码：便于前端国际化展示与重试判断</summary>
        public string ErrorCode { get; protected set; }

        /// <summary>用户友好的错误消息（已去除敏感信息）</summary>
        public string UserMessage { get; protected set; }

        protected AiException(string errorCategory, string errorCode, string message, string userMessage = null)
            : base(message)
        {
            ErrorCategory = errorCategory ?? "unknown";
            ErrorCode = errorCode ?? "unknown";
            UserMessage = userMessage ?? message;
        }
    }

    /// <summary>
    /// HTTP 请求异常：网络错误、服务不可达等。
    /// </summary>
    public sealed class AiHttpException : AiException
    {
        /// <summary>HTTP 状态码（可能为 null，表示无响应）</summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>服务方返回的请求 ID，便于远端日志关联</summary>
        public string ProviderRequestId { get; set; }

        /// <summary>是否可重试（4xx 中某些、5xx 等可重试；401 等不可重试）</summary>
        public bool IsRetryable
        {
            get
            {
                if (HttpStatusCode == null) return true; // 无响应，可重试
                var code = (HttpStatusCode ?? 0);
                // 4xx 中：超时、限流、服务不可用时可重试；401/403/404 不可重试
                return code >= 500 || code == 408 || code == 429 || code == 503 || code == 504;
            }
        }

        public AiHttpException(
            int? statusCode,
            string errorCode,
            string message,
            string userMessage = null,
            string providerRequestId = null)
            : base("http", errorCode, message, userMessage ?? "AI 服务调用失败，请稍后重试")
        {
            HttpStatusCode = statusCode;
            ProviderRequestId = providerRequestId;
        }
    }

    /// <summary>
    /// 超时异常：请求或流式响应超时。
    /// </summary>
    public sealed class AiTimeoutException : AiException
    {
        /// <summary>超时配置的秒数</summary>
        public int TimeoutSeconds { get; set; }

        /// <summary>实际经过的毫秒数</summary>
        public long ElapsedMilliseconds { get; set; }

        public AiTimeoutException(int timeoutSeconds, long elapsedMs, string message = null)
            : base("timeout", "timeout_exceeded", 
                message ?? $"AI 服务响应超时（配置 {timeoutSeconds}s，已等待 {elapsedMs}ms）",
                "AI 服务响应超时，请稍后重试")
        {
            TimeoutSeconds = timeoutSeconds;
            ElapsedMilliseconds = elapsedMs;
        }
    }

    /// <summary>
    /// 限流异常：API 返回 429 Too Many Requests。
    /// </summary>
    public sealed class AiRateLimitException : AiException
    {
        /// <summary>建议的重试延迟（秒），来自 Retry-After 响应头</summary>
        public int? RetryAfterSeconds { get; set; }

        /// <summary>限流原因：qpm（每分钟请求数）/ tpm（每分钟 token 数）等</summary>
        public string LimitType { get; set; }

        public AiRateLimitException(
            string message = null,
            int? retryAfterSeconds = null,
            string limitType = null)
            : base("rate_limit", "rate_limit_exceeded",
                message ?? "调用 AI 服务过于频繁，已触发限流",
                "请求过于频繁，请稍后再试")
        {
            RetryAfterSeconds = retryAfterSeconds;
            LimitType = limitType;
        }
    }

    /// <summary>
    /// 配置异常：缺失/无效的配置参数。
    /// </summary>
    public sealed class AiConfigurationException : AiException
    {
        /// <summary>缺失或无效的配置字段名</summary>
        public string ConfigField { get; set; }

        public AiConfigurationException(string configField, string message, string userMessage = null)
            : base("config", "config_invalid",
                message ?? $"AI 配置异常：{configField}",
                userMessage ?? "AI 服务配置不完整，请联系管理员")
        {
            ConfigField = configField;
        }
    }

    /// <summary>
    /// 响应解析异常：HTTP 200 但响应格式非预期（JSON 格式错误、缺失字段等）。
    /// </summary>
    public sealed class AiResponseException : AiException
    {
        /// <summary>实际收到的响应内容（截断处理，防止日志过大）</summary>
        public string ResponseContent { get; set; }

        public AiResponseException(
            string message,
            string responseContent = null)
            : base("response", "response_parse_error",
                message ?? "AI 服务返回了无法解析的响应",
                "AI 服务返回异常，请稍后重试或联系管理员")
        {
            ResponseContent = responseContent;
        }
    }

    /// <summary>
    /// 用户中止异常：客户端主动取消了 AI 请求（如流式关闭连接）。
    /// </summary>
    public sealed class AiCancelledException : AiException
    {
        /// <summary>取消原因：user_cancelled / timeout_cascade 等</summary>
        public string CancelReason { get; set; }

        public AiCancelledException(string reason = "user_cancelled", long? elapsedMs = null)
            : base("cancelled", reason,
                $"AI 请求已中止（原因：{reason}{(elapsedMs.HasValue ? $"，已处理 {elapsedMs}ms" : string.Empty)}）",
                "请求已取消")
        {
            CancelReason = reason;
        }
    }
}
