using System;
using System.Collections.Generic;
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
