using Infrastructure;
using Infrastructure.Model;
using IPTools.Core;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using NLog;
using System;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 调用 app.UseMiddleware<GlobalExceptionMiddleware>();
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
            LogLevel logLevel = LogLevel.Info;
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
                logLevel = LogLevel.Error;
                context.Response.StatusCode = 500;
            }

            string responseResult = System.Text.Json.JsonSerializer.Serialize(new ApiResult(code, msg)).ToLower();
            string ip = HttpContextExtension.GetClientUserIp(context);
            var ip_info = IpTool.Search(ip);

            SysOperLog sysOperLog = new()
            {
                status = 1,
                operIp = ip,
                operUrl = HttpContextExtension.GetRequestUrl(context),
                requestMethod = context.Request.Method,
                jsonResult = responseResult,
                errorMsg = string.IsNullOrEmpty(error) ? msg : error,
                operName = context.User.Identity.Name,
                operLocation = ip_info.Province + " " + ip_info.City,
                operTime = DateTime.Now
            };
            HttpContextExtension.GetRequestValue(context, sysOperLog);
            LogEventInfo ei = new(logLevel, "GlobalExceptionMiddleware", error);

            ei.Exception = ex;
            ei.Message = error;
            ei.Properties["status"] = 1;//走正常返回都是通过走GlobalExceptionFilter不通过
            ei.Properties["jsonResult"] = responseResult;
            ei.Properties["requestParam"] = sysOperLog.operParam;
            ei.Properties["user"] = context.User.Identity.Name;

            Logger.Log(ei);
            await context.Response.WriteAsync(responseResult);

            SysOperLogService.InsertOperlog(sysOperLog);
        }
    }
}
