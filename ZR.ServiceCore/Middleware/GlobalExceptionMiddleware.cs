using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NLog;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ZR.Common;
using ZR.Infrastructure.IPTools;
using ZR.Model.System;
using ZR.ServiceCore.Services;
using textJson = System.Text.Json;

namespace ZR.ServiceCore.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 调用 app.UseMiddlewareGlobalExceptionMiddleware>();
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ISysOperLogService SysOperLogService;

        static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly textJson.JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = textJson.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="next"></param>
        /// <param name="sysOperLog"></param>
        public GlobalExceptionMiddleware(RequestDelegate next, ISysOperLogService sysOperLog)
        {
            this.next = next;
            this.SysOperLogService = sysOperLog;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            LogLevel logLevel = LogLevel.Info;
            int code = (int)ResultCode.GLOBAL_ERROR;
            string msg;
            string error = string.Empty;
            bool notice = true;
            //自定义异常
            if (ex is CustomException customException)
            {
                code = customException.Code;
                msg = customException.Message;
                error = customException.LogMsg;
                notice = customException.Notice;
            }
            else if (ex is ArgumentException)//参数异常
            {
                code = (int)ResultCode.PARAM_ERROR;
                msg = ex.Message;
            }
            else
            {
                var q1 = "Exception has been thrown by the target of an invocation";
                var an1 = string.Empty;
                if (ex.Message.Contains(q1))
                {
                    an1 = $"====请查看issue：https://gitee.com/izory/ZrAdminNetCore/issues/I6S4DZ";
                }
                msg = "服务器好像出了点问题，请联系系统管理员...";
                error = $"异常原因：{ex.Message}{an1}";
                logLevel = LogLevel.Error;
                context.Response.StatusCode = 500;
            }

            ApiResult apiResult = new(code, msg);
#if DEBUG
            if (logLevel == LogLevel.Error)
            {
                apiResult.Add("error", error);
            }
#endif
            string responseResult = textJson.JsonSerializer.Serialize(apiResult, JsonOptions);
            string ip = HttpContextExtension.GetClientUserIp(context);
            var ip_info = IpTool.Search(ip);
            string operLocation = ip_info == null ? string.Empty : $"{ip_info.Province} {ip_info.City}";
            string errorMessage = string.IsNullOrWhiteSpace(error) ? msg : error;
            // 写入数据库操作日志 / 推送企业微信时做脱敏，避免泄露连接字符串、凭据及内网 IP
            string safeErrorMessage = SanitizeErrorMessage(errorMessage);

            SysOperLog sysOperLog = new()
            {
                Status = 1,
                OperIp = ip,
                OperUrl = HttpContextExtension.GetRequestUrl(context),
                RequestMethod = context.Request.Method,
                JsonResult = responseResult,
                ErrorMsg = safeErrorMessage,
                OperName = HttpContextExtension.GetName(context),
                OperLocation = operLocation,
                OperTime = DateTime.Now,
                OperParam = HttpContextExtension.GetRequestValue(context, context.Request.Method)
            };
            var endpoint = GetEndpoint(context);
            if (endpoint != null)
            {
                var logAttribute = endpoint.Metadata.GetMetadata<LogAttribute>();
                if (logAttribute != null)
                {
                    sysOperLog.BusinessType = (int)logAttribute.BusinessType;
                    sysOperLog.Title = logAttribute?.Title;
                    sysOperLog.OperParam = logAttribute.IsSaveRequestData ? sysOperLog.OperParam : "";
                    sysOperLog.JsonResult = logAttribute.IsSaveResponseData ? sysOperLog.JsonResult : "";
                }
            }
            LogEventInfo ei = new(logLevel, "GlobalExceptionMiddleware", errorMessage)
            {
                Exception = ex,
                Message = errorMessage
            };
            ei.Properties["status"] = 1;//走正常返回都是通过走GlobalExceptionFilter不通过
            ei.Properties["jsonResult"] = responseResult;
            ei.Properties["requestParam"] = sysOperLog.OperParam;
            ei.Properties["user"] = sysOperLog.OperName;

            Logger.Log(ei);
            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = "application/json;charset=utf-8";
                await context.Response.WriteAsync(responseResult, System.Text.Encoding.UTF8);
            }

            string errorMsg = $"> 操作人：{sysOperLog.OperName}" +
                $"\n> 操作地区：{sysOperLog.OperIp}({sysOperLog.OperLocation})" +
                $"\n> 操作模块：{sysOperLog.Title}" +
                $"\n> 操作地址：{sysOperLog.OperUrl}" +
                $"\n> 错误信息：{msg}\n\n> {safeErrorMessage}";

            try
            {
                SysOperLogService.InsertOperlog(sysOperLog);
            }
            catch (Exception logEx)
            {
                Logger.Error(logEx, "记录操作日志失败");
            }

            if (!notice) return;
            try
            {
                WxNoticeHelper.SendMsg("系统异常", errorMsg, msgType: WxNoticeHelper.MsgType.markdown);
            }
            catch (Exception noticeEx)
            {
                Logger.Error(noticeEx, "发送异常通知失败");
            }
        }

        /// <summary>
        /// 脱敏异常信息：抹掉连接字符串中的敏感键值(密码/账号/库名等)、内网 IP，
        /// 防止数据库连不上等异常将 Server=xxx;Password=xxx 或内网地址泄露到操作日志/通知中。
        /// 服务端 NLog 仍记录完整信息用于排错。
        /// </summary>
        private static readonly Regex ConnStrKeyRegex = new(
            @"(\b(?:Server|Data\s*Source|Address|Addr|Network\s*Address|Database|Initial\s*Catalog|User\s*ID|Uid|User|Password|Pwd|Integrated\s*Security)\s*=\s*)([^;""']+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IpRegex = new(
            @"\b(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?\b",
            RegexOptions.Compiled);

        private static string SanitizeErrorMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return message;
            message = ConnStrKeyRegex.Replace(message, m => $"{m.Groups[1].Value}***");
            message = IpRegex.Replace(message, "***.***.***.***");
            return message;
        }

        public static Endpoint GetEndpoint(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.Features.Get<IEndpointFeature>()?.Endpoint;
        }
    }
}
