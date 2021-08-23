using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ZRAdmin.Middleware
{
    public class RequestIPMiddleware
    {
        private readonly RequestDelegate _next;

        static readonly Logger Logger = LogManager.GetCurrentClassLogger();//声明NLog变量

        public RequestIPMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var url = context.Request.Path.ToString().ToLower();
            string userip = Tools.GetRealIP();
            //string agent = HttpContextExtension.GetUserAgent(context);

            string[] urls = new string[] { "/css", "/js", "/images", "/lib", "/home/error", "/api" };

            var strRegex = "(.jpg|.png|.gif|.php|.cfg|.ico)$"; //用于验证图片扩展名的正则表达式
            var re = new Regex(strRegex);

            //阻止.php访问往下请求
            if (new Regex("(.php)$").IsMatch(url))
            {
                await context.Response.WriteAsync("hello");
                return;
            }

            //var ip_info = IpTool.Search(userip);

            ////bool flag = ((IList)urls).Contains(url);
            //if (!re.IsMatch(url) )
            //{
            Logger.Debug($"IP中间件请求访问，IP[{userip}]");
            //}

            //两种方式传递下去 一是invoke 一个直接next
            //await _next.Invoke(context);

            await _next(context);
        }
    }

    public static class RequestIPMiddlewareExtensions
    {
        /// <summary>
        /// 用于引用请求IP中间件
        /// </summary>
        /// <param name="builder">扩展类型</param>
        /// <returns>IApplicationBuilder</returns>
        public static IApplicationBuilder UseRequestIPMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestIPMiddleware>();
        }
    }
}
