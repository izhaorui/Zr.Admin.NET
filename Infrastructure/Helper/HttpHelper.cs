using System;
using System.Collections.Generic;
using System.IO;
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
        /// </summary>
        private static readonly HttpClient SharedClient = new HttpClient();

        /// <summary>
        /// 构建并发送请求：超时通过 CancellationToken 控制，请求级 header 仅作用于本次请求。
        /// </summary>
        private static async Task<string> SendAsync(HttpRequestMessage request, int timeOut, Dictionary<string, string> headers)
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

            using var cts = new CancellationTokenSource(timeOut * 1000);
            HttpResponseMessage response = await SharedClient.SendAsync(request, cts.Token);
            return await response.Content.ReadAsStringAsync();
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
            postData ??= "";
            using var httpContent = new StringContent(postData, Encoding.UTF8);
            if (contentType != null)
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = httpContent };
            return await SendAsync(request, timeOut, headers);
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
        public static async Task<string> HttpGetAsync(string url, Dictionary<string, string> headers = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request, 30, headers);
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
