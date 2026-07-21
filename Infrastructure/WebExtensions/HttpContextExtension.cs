using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UAParser;
using ZR.Common;
using ZR.Infrastructure.IPTools;

namespace Infrastructure.Extensions
{
    /// <summary>
    /// HttpContext扩展类
    /// </summary>
    public static partial class HttpContextExtension
    {
        /// <summary>
        /// HttpContext.Items 中缓存的当前登录用户 Key（JwtAuthMiddleware 写入，避免同请求内重复解析 JWT）
        /// </summary>
        public const string CurrentUserCacheKey = "__CurrentUser";
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

            return request.Headers["X-Requested-With"] == "XMLHttpRequest" || request.Headers != null && request.Headers["X-Requested-With"] == "XMLHttpRequest";
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
                result = context.Connection.RemoteIpAddress?.ToString();
            }
            if (string.IsNullOrEmpty(result))
                throw new Exception("获取IP失败");

            if (result.Contains("::1"))
                result = "127.0.0.1";

            result = result.Replace("::ffff:", "");
            result = result.Split(':')?.FirstOrDefault() ?? "127.0.0.1";
            result = IsIP(result) ? result : "127.0.0.1";
            return result;
        }

        /// <summary>
        /// 判断是否IP
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool IsIP(string ip)
        {
            return Regex.IsMatch(ip, @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$");
        }

        /// <summary>
        /// 获取登录用户id
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static long GetUId(this HttpContext context)
        {
            return GetCurrentUser(context)?.UserId ?? 0;
        }

        /// <summary>
        /// 获取部门id
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static long GetDeptId(this HttpContext context)
        {
            return GetCurrentUser(context)?.DeptId ?? 0;
        }

        /// <summary>
        /// 获取登录用户名
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetName(this HttpContext context)
        {
            return GetCurrentUser(context)?.UserName ?? string.Empty;
        }

        /// <summary>
        /// 判断是否是管理员
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static bool IsAdmin(this HttpContext context)
        {
            var userName = context.GetName();
            return userName == GlobalConstant.AdminRole;
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

        /// <summary>
        /// 获取租户ID
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetTenantId(this HttpContext context)
        {
            return context.Request.Headers["tenantId"];
        }

        /// <summary>
        /// 获取请求Url
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetRequestUrl(this HttpContext context)
        {
            return context != null ? context.Request.Path.Value : "";
        }

        /// <summary>
        /// 获取请求参数
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetQueryString(this HttpContext context)
        {
            return context != null ? context.Request.QueryString.Value : "";
        }

        /// <summary>
        /// 获取body请求参数
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string GetBody(this HttpContext context)
        {
            context.Request.EnableBuffering();
            //context.Request.Body.Seek(0, SeekOrigin.Begin);
            //using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            ////需要使用异步方式才能获取
            //return reader.ReadToEndAsync().Result;
            string body = string.Empty;
            var buffer = new MemoryStream();
            context.Request.Body.Seek(0, SeekOrigin.Begin);
            context.Request.Body.CopyToAsync(buffer);
            buffer.Position = 0;
            try
            {
                using StreamReader streamReader = new(buffer, Encoding.UTF8);
                body = streamReader.ReadToEndAsync().Result;
            }
            finally
            {
                buffer?.Dispose();
            }
            return body;
        }

        /// <summary>
        /// 获取body请求参数（异步）
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task<string> GetBodyAsync(this HttpContext context)
        {
            // 允许多次读取请求体
            context.Request.EnableBuffering();

            // 重置请求体流的位置，确保从头读取
            context.Request.Body.Position = 0;

            // 读取请求体内容
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            // 读取完成后将流位置重置为0，确保后续操作可以继续读取
            context.Request.Body.Position = 0;

            return body;
        }

        /// <summary>
        /// 获取浏览器信息
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static ClientInfo GetClientInfo(this HttpContext context)
        {
            var str = context.GetUserAgent();
            var uaParser = Parser.GetDefault();
            ClientInfo c = uaParser.Parse(str);

            return c;
        }

        /// <summary>
        /// 根据IP获取地理位置
        /// </summary>
        /// <returns></returns>
        public static string GetIpInfo(string IP)
        {
            var ipInfo = IpTool.Search(IP);
            return ipInfo?.Province + "-" + ipInfo?.City + "-" + ipInfo?.NetworkOperator;
        }

        /// <summary>
        /// 设置请求参数
        /// </summary>
        /// <param name="reqMethod"></param>
        /// <param name="context"></param>
        public static string GetRequestValue(this HttpContext context, string reqMethod)
        {
            string param = string.Empty;

            if (HttpMethods.IsPost(reqMethod) || HttpMethods.IsPut(reqMethod) || HttpMethods.IsDelete(reqMethod))
            {
                param = context.GetBody();
                string regex = "(?<=\"password\":\")[^\",]*";
                param = Regex.Replace(param, regex, "***");
            }
            if (param.IsEmpty())
            {
                param = context.GetQueryString();
            }
            return param;
        }

        /// <summary>
        /// 获取当前用户登录信息
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static LoginUser GetCurrentUser(this HttpContext context)
        {
            // 优先从 HttpContext.Items 获取（JwtAuthMiddleware 已解析并存入，避免重复解析 JWT）
            if (context.Items.TryGetValue(CurrentUserCacheKey, out var cached) && cached is LoginUser tokenModel)
            {
                tokenModel.Permissions ??= (List<string>)CacheHelper.GetCache(GlobalConstant.UserPermKEY + tokenModel.UserId) ?? [];
                return tokenModel;
            }

            // 回退：匿名/白名单等未经过 JwtAuthMiddleware 的场景，直接解析 JWT
            tokenModel = JwtUtil.GetLoginUser(context);
            if (tokenModel != null)
            {
                tokenModel.Permissions = (List<string>)CacheHelper.GetCache(GlobalConstant.UserPermKEY + tokenModel.UserId) ?? [];
                context.Items[CurrentUserCacheKey] = tokenModel;
            }
            return tokenModel;
        }

        /// <summary>
        /// 是否有敏感数据权限
        /// </summary>
        /// <param name="context"></param>
        /// <param name="perm"></param>
        /// <returns></returns>
        public static bool HasSensitivePerm(this HttpContext context, string perm)
        {
            if (IsAdmin(context)) return true;
            var perms = context.User?.FindFirst("sensitivePerms")?.Value?.Split(',') ?? [];
            return perms.Contains(perm);
        }
    }
}
