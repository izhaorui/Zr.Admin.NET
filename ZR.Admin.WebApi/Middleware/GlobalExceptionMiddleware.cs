using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using IPTools.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NLog;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Extensions;
using ZR.Common;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 调用 app.UseMiddlewareGlobalExceptionMiddleware>();
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ISysOperLogService SysOperLogService;

        static readonly Logger Logger = LogManager.GetCurrentClassLogger();//声明NLog变量

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
            NLog.LogLevel logLevel = NLog.LogLevel.Info;
            int code = (int)ResultCode.GLOBAL_ERROR;
            string msg;
            string error = string.Empty;
            //自定义异常
            if (ex is CustomException customException)
            {
                code = customException.Code;
                msg = customException.Message;
                error = customException.LogMsg;
            }
            else if (ex is ArgumentException)//参数异常
            {
                code = (int)ResultCode.PARAM_ERROR;
                msg = ex.Message;
            }
            else
            {
                msg = "服务器好像出了点问题......";
                error = $"{ex.Message}";
                logLevel = NLog.LogLevel.Error;
                context.Response.StatusCode = 500;
            }
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            ApiResult apiResult = new(code, msg);
            string responseResult = JsonSerializer.Serialize(apiResult, options).ToLower();
            string ip = HttpContextExtension.GetClientUserIp(context);
            var ip_info = IpTool.Search(ip);

            SysOperLog sysOperLog = new()
            {
                Status = 1,
                OperIp = ip,
                OperUrl = HttpContextExtension.GetRequestUrl(context),
                RequestMethod = context.Request.Method,
                JsonResult = responseResult,
                ErrorMsg = string.IsNullOrEmpty(error) ? msg : error,
                OperName = context.User.Identity.Name,
                OperLocation = ip_info.Province + " " + ip_info.City,
                OperTime = DateTime.Now
            };
            HttpContextExtension.GetRequestValue(context, sysOperLog);
            var endpoint = GetEndpoint(context);
            if (endpoint != null)
            {
                var logAttribute = endpoint.Metadata.GetMetadata<LogAttribute>();
                if (logAttribute != null)
                {
                    sysOperLog.BusinessType = (int)logAttribute?.BusinessType;
                    sysOperLog.Title = logAttribute?.Title;
                    sysOperLog.OperParam = logAttribute.IsSaveRequestData ? sysOperLog.OperParam : "";
                    sysOperLog.JsonResult = logAttribute.IsSaveResponseData ? sysOperLog.JsonResult : "";
                }
            }
            LogEventInfo ei = new(logLevel, "GlobalExceptionMiddleware", error)
            {
                Exception = ex,
                Message = error
            };
            ei.Properties["status"] = 1;//走正常返回都是通过走GlobalExceptionFilter不通过
            ei.Properties["jsonResult"] = responseResult;
            ei.Properties["requestParam"] = sysOperLog.OperParam;
            ei.Properties["user"] = HttpContextExtension.GetName(context);

            Logger.Log(ei);
            context.Response.ContentType = "text/json;charset=utf-8";
            await context.Response.WriteAsync(responseResult, System.Text.Encoding.UTF8);
            WxNoticeHelper.SendMsg("系统出错", sysOperLog.ErrorMsg);
            SysOperLogService.InsertOperlog(sysOperLog);
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
