using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UAParser;
using ZR.Model.System;

namespace ZR.Admin.WebApi.Extensions
{
    /// <summary>
    /// HttpContext扩展类
    /// </summary>
    public static class HttpContextExtension
    {
        /// <summary>
        /// 是否是ajax请求
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            //return request.Headers.ContainsKey("X-Requested-With") &&
            //       request.Headers["X-Requested-With"].Equals("XMLHttpRequest");

            return request.Headers["X-Requested-With"] == "XMLHttpRequest" || (request.Headers != null && request.Headers["X-Requested-With"] == "XMLHttpRequest");
        }

        /// <summary>
        /// 获取客户端IP
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetClientUserIp(this HttpContext context)
        {
            if (context == null) return "";
            var result = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(result))
            {
                result = context.Connection.RemoteIpAddress.ToString();
            }
            if (string.IsNullOrEmpty(result) || result.Contains("::1"))
                result = "127.0.0.1";

            result = result.Replace("::ffff:", "127.0.0.1");
            result = IsIP(result) ? result : "127.0.0.1";
            return result;
        }

        public static bool IsIP(string ip)
        {
            return Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
        }

        public static long GetUId(this HttpContext context)
        {
            var uid = context.User.FindFirstValue(ClaimTypes.PrimarySid);

            return !string.IsNullOrEmpty(uid) ? long.Parse(uid) : 0;
        }
        public static string GetName(this HttpContext context)
        {
            var uid = context.User?.Identity?.Name;

            return uid;
        }

        /// <summary>
        /// ClaimsIdentity
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IEnumerable<ClaimsIdentity> GetClaims(this HttpContext context)
        {
            return context.User?.Identities;
        }
        //public static int GetRole(this HttpContext context)
        //{
        //    var roleid = context.User.FindFirstValue(ClaimTypes.Role) ?? "0";

        //    return int.Parse(roleid);
        //}

        public static string GetUserAgent(this HttpContext context)
        {
            return context.Request.Headers["User-Agent"];
        }

        /// <summary>
        /// 获取请求令牌
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetToken(this HttpContext context)
        {
            return context.Request.Headers["Authorization"];
        }

        public static ClientInfo GetClientInfo(this HttpContext context)
        {
            var str = GetUserAgent(context);
            var uaParser = Parser.GetDefault();
            ClientInfo c = uaParser.Parse(str);

            return c;
        }

        public static string GetRequestUrl(this HttpContext context)
        {
            return context != null ? context.Request.Path.Value : "";
        }

        /// <summary>
        ///组装Claims
        /// </summary>
        /// <param name="context"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static List<Claim> AddClaims(this HttpContext context, LoginUser user)
        {
            //1、创建Cookie保存用户信息，使用claim
            var claims = new List<Claim>()
                {
                    new Claim(ClaimTypes.PrimarySid, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.UserData, JsonConvert.SerializeObject(user))
                };

            //写入Cookie
            //WhiteCookie(context, claims);
            return claims;
        }

        private static void WhiteCookie(HttpContext context, List<Claim> claims)
        {
            //2.创建声明主题 指定认证方式 这里使用cookie
            var claimsIdentity = new ClaimsIdentity(claims, "Login");

            Task.Run(async () =>
            {
                await context.SignInAsync(
                JwtBearerDefaults.AuthenticationScheme,//这里要注意的是HttpContext.SignInAsync(AuthenticationType,…) 所设置的Scheme一定要与前面的配置一样，这样对应的登录授权才会生效。
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties()
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.Now.AddDays(1),//有效时间
            });
            }).Wait();
        }


        /// <summary>
        /// 设置请求参数
        /// </summary>
        /// <param name="operLog"></param>
        /// <param name="context"></param>
        public static void GetRequestValue(this HttpContext context, SysOperLog operLog)
        {
            string reqMethod = operLog.requestMethod;
            string param;

            if (HttpMethods.IsPost(reqMethod) || HttpMethods.IsPut(reqMethod))
            {
                context.Request.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                //需要使用异步方式才能获取
                param = reader.ReadToEndAsync().Result;
            }
            else
            {
                param = context.Request.QueryString.Value.ToString();
            }
            operLog.operParam = param;
        }

    }

}
