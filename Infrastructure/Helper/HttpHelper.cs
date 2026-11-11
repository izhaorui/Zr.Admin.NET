using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class HttpHelper
    {
        /// <summary>
        /// 静态单例 HttpClient：避免每次请求 new HttpClient 导致 socket（TIME_WAIT）耗尽。
        /// 所有对外 POST/GET 共享此实例；注意不可在发送请求后修改其实例级可变属性
        /// （Timeout / DefaultRequestHeaders），否则会抛
        /// "This instance has already started one or more requests" 异常。
        /// 因此：超时由各调用方通过 timeOut 参数生成 CancellationToken 控制，
        /// 请求级 header 写入每次新建的 HttpRequestMessage.Headers，绝不污染共享实例。
        /// Timeout 设为 Infinite：避免默认 100s 与业务 CTS（如 Ollama 120s）互相抢短，导致“无响应”。
        /// </summary>
        private static readonly HttpClient SharedClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>
        /// 构建并发送请求：超时通过 CancellationToken 控制，请求级 header 仅作用于本次请求。
        /// </summary>
        private static async Task<string> SendAsync(
            HttpRequestMessage request,
            int timeOut,
            Dictionary<string, string> headers,
            CancellationToken cancellationToken = default)
        {
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    // 跳过 content 类型相关头（由 HttpContent 提供），其余写入请求头
                    if (!request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value) ?? false)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            using var timeoutCts = new CancellationTokenSource(timeOut * 1000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            using var response = await SharedClient.SendAsync(request, linkedCts.Token);
            return await response.Content.ReadAsStringAsync(linkedCts.Token);
        }

        /// <summary>
        /// 发起POST同步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="contentType">application/xml、application/json、application/text、application/x-www-form-urlencoded</param>
        /// <param name="headers">填充消息头</param>        
        /// <returns></returns>
        public static string HttpPost(string url, string postData = null, string contentType = null, int timeOut = 30, Dictionary<string, string> headers = null)
        {
            postData ??= "";
            using var httpContent = new StringContent(postData, Encoding.UTF8);
            if (contentType != null)
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
            return SendAsync(request, timeOut, headers).Result;
        }

        /// <summary>
        /// 发起POST异步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="contentType">application/xml、application/json、application/text、application/x-www-form-urlencoded</param>
        /// <param name="headers">填充消息头</param>        
        /// <returns></returns>
        public static async Task<string> HttpPostAsync(string url, string postData = null, string contentType = null, int timeOut = 30, Dictionary<string, string> headers = null)
        {
            return await HttpPostAsync(url, postData, contentType, timeOut, headers, CancellationToken.None);
        }

        public static async Task<string> HttpPostAsync(
            string url,
            string postData,
            string contentType,
            int timeOut,
            Dictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            postData ??= "";
            using var httpContent = new StringContent(postData, Encoding.UTF8);
            if (contentType != null)
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
            return await SendAsync(request, timeOut, headers, cancellationToken);
        }

        public sealed class HttpTextResponse
        {
            public string Content { get; set; }
            public int StatusCode { get; set; }
            public string RequestId { get; set; }
            public bool IsSuccessStatusCode { get; set; }

            /// <summary>
            /// 服务端要求的重试等待秒数（Retry-After，429 限流时常见）。取不到为 null。
            /// </summary>
            public int? RetryAfterSeconds { get; set; }
        }

        /// <summary>
        /// AI 等需要审计 HTTP 状态与 Provider 请求 ID 的详细 POST。
        /// 保留原 HttpPostAsync 行为，避免影响其他调用方。
        /// </summary>
        public static async Task<HttpTextResponse> HttpPostDetailedAsync(
            string url, string postData = null, string contentType = null, int timeOut = 30,
            Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            postData ??= "";
            using var httpContent = new StringContent(postData, Encoding.UTF8);
            if (contentType != null)
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (!request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value) ?? false)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            using var timeoutCts = new CancellationTokenSource(Math.Max(1, timeOut) * 1000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            using var response = await SharedClient.SendAsync(request, linkedCts.Token);
            var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return new HttpTextResponse
            {
                Content = body,
                StatusCode = (int)response.StatusCode,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                RequestId = ReadRequestId(response),
                RetryAfterSeconds = ReadRetryAfterSeconds(response)
            };
        }

        public static string ReadRequestId(HttpResponseMessage response)
        {
            if (response == null) return null;
            foreach (var name in new[] { "x-request-id", "request-id", "x-trace-id", "trace-id" })
            {
                if (response.Headers.TryGetValues(name, out var values))
                {
                    return values.FirstOrDefault();
                }
            }
            return null;
        }

        /// <summary>
        /// 读取 Retry-After 响应头的等待秒数，支持"秒数"与"HTTP-date"两种规范写法；
        /// 缺失、格式非法或已过期时返回 null（表示服务端未给出建议值）。
        /// </summary>
        public static int? ReadRetryAfterSeconds(HttpResponseMessage response)
        {
            var retryAfter = response?.Headers?.RetryAfter;
            if (retryAfter == null) return null;

            if (retryAfter.Delta.HasValue)
            {
                var seconds = (int)Math.Ceiling(retryAfter.Delta.Value.TotalSeconds);
                return seconds > 0 ? seconds : null;
            }

            if (retryAfter.Date.HasValue)
            {
                var seconds = (int)Math.Ceiling((retryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds);
                return seconds > 0 ? seconds : null;
            }

            return null;
        }

        /// <summary>
        /// 发起GET同步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static string HttpGet(string url, Dictionary<string, string> headers = null)
        {
            var requestHeaders = headers;
            if (requestHeaders == null)
            {
                requestHeaders = new Dictionary<string, string>
                {
                    { "ContentType", "application/x-www-form-urlencoded" },
                    { "UserAgent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)" }
                };
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            try
            {
                return SendAsync(request, 30, requestHeaders).Result;
            }
            catch (Exception ex)
            {
                //TODO 打印日志
                Console.WriteLine($"[Http请求出错]{url}|{ex.Message}");
            }
            return "";
        }

        /// <summary>
        /// 发起GET异步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static async Task<string> HttpGetAsync(string url, Dictionary<string, string> headers = null, int timeOut = 30)
        {
            return await HttpGetAsync(url, headers, timeOut, CancellationToken.None);
        }

        public static async Task<string> HttpGetAsync(
            string url,
            Dictionary<string, string> headers,
            int timeOut,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request, timeOut, headers, cancellationToken);
        }

        /// <summary>
        /// 带状态码的 GET 详细版：供 AI 拉取模型列表等需要区分 401/404 的场景使用。
        /// 与 <see cref="HttpPostDetailedAsync"/> 对齐——请求级 header 只写本次请求，超时用独立 CTS 控制，
        /// 不污染共享的 <see cref="SharedClient"/>。
        /// </summary>
        public static async Task<HttpTextResponse> HttpGetDetailedAsync(
            string url, int timeOut = 30, Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var timeoutCts = new CancellationTokenSource(Math.Max(1, timeOut) * 1000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            using var response = await SharedClient.SendAsync(request, linkedCts.Token);
            var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return new HttpTextResponse
            {
                Content = body,
                StatusCode = (int)response.StatusCode,
                IsSuccessStatusCode = response.IsSuccessStatusCode,
                RequestId = ReadRequestId(response),
                RetryAfterSeconds = ReadRetryAfterSeconds(response)
            };
        }

        /// <summary>
        /// 下载文件为二进制字节数组，带大小上限与超时保护。用于 AI 附件（图片/文档）解析前的取数。
        /// 超出 maxBytes 或下载失败/超时抛出 InvalidOperationException，由调用方降级处理（如"仅列文件名"）。
        /// </summary>
        /// <param name="url">完整 http(s) 地址</param>
        /// <param name="timeOut">超时秒数，默认 30</param>
        /// <param name="maxBytes">字节上限，默认 20MB；超过直接拒绝，避免大文件拖垮 AI 请求</param>
        public static async Task<byte[]> HttpDownloadBytesAsync(string url, int timeOut = 30, int maxBytes = 20 * 1024 * 1024)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var cts = new CancellationTokenSource(timeOut * 1000);
            using var response = await SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > maxBytes)
            {
                throw new InvalidOperationException($"文件过大（{response.Content.Headers.ContentLength.Value} 字节），超过上限 {maxBytes} 字节，已跳过下载。");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            long total = 0;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    throw new InvalidOperationException($"文件下载超过上限 {maxBytes} 字节，已中断。");
                }
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// 流式 HTTP 响应。用完务必 Dispose 以释放底层响应与生命周期令牌。
        /// <para>注意：本类<b>不再承担超时职责</b>。超时由调用方按读取节奏自行控制
        /// （AI 场景为"空闲超时 + 总时长硬上限"，见 <c>AiLlmClient.StreamChatWithToolsAsync</c>），
        /// 否则长回答会被"从发请求开始算的总时长"误杀。</para>
        /// </summary>
        public sealed class HttpStreamResponse : IDisposable
        {
            private readonly CancellationTokenSource _lifetimeCts = new();

            /// <summary>完整响应（含状态码、Headers，Content 通过 ReadAsStreamAsync 读）</summary>
            public HttpResponseMessage Response { get; }

            /// <summary>生命周期令牌：仅随 Dispose 取消，不参与超时判定。</summary>
            public CancellationToken Token => _lifetimeCts.Token;

            public HttpStreamResponse(HttpResponseMessage response)
            {
                Response = response;
            }

            public void Dispose()
            {
                try
                {
                    _lifetimeCts.Cancel();
                }
                catch
                {
                    // 已释放或取消已在进行中：忽略
                }
                Response?.Dispose();
                _lifetimeCts.Dispose();
            }
        }

        /// <summary>
        /// 发起流式读取 POST（SSE / 下载等）。以 ResponseHeadersRead 模式返回，
        /// 响应体不预先整体读入内存。
        /// <para><paramref name="timeOut"/> 只约束"建立连接 + 等到响应头"这一段；拿到响应头后计时器即释放，
        /// 响应体读取阶段的超时由调用方按业务节奏控制（如 SSE 的空闲超时）。</para>
        /// </summary>
        public static async Task<HttpStreamResponse> HttpPostReadStreamAsync(string url, string postData = null, string contentType = null, int timeOut = 30, Dictionary<string, string> headers = null, CancellationToken cancellationToken = default)
        {
            postData ??= "";
            using var httpContent = new StringContent(postData, Encoding.UTF8);
            if (contentType != null)
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (!request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value) ?? false)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            // 仅约束发送与响应头阶段；using 保证拿到头（或失败）后立即停表，不会延续到读取阶段。
            using var sendCts = new CancellationTokenSource(Math.Max(1, timeOut) * 1000);
            using var linkedSendCts = CancellationTokenSource.CreateLinkedTokenSource(sendCts.Token, cancellationToken);
            var response = await SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedSendCts.Token);
            return new HttpStreamResponse(response);
        }

        /// <summary>
        /// 发起Put同步请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="contentType">application/xml、application/json、application/text、application/x-www-form-urlencoded</param>
        /// <param name="headers">填充消息头</param>        
        /// <returns></returns>
        public static string HttpPut(string url, string postData = null, string contentType = null, int timeOut = 30, Dictionary<string, string> headers = null)
        {
            postData ??= "";
            using var httpContent = new StringContent(postData, Encoding.UTF8);
            if (contentType != null)
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = httpContent };
            return SendAsync(request, timeOut, headers).Result;
        }
    }
}
