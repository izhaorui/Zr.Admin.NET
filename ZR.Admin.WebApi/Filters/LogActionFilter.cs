using Infrastructure;
using Infrastructure.Attribute;
using IPTools.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Text;
using ZR.Admin.WebApi.Extensions;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Filters
{
    /// <summary>
    /// 记录请求，输出日志
    /// </summary>
    public class LogActionFilter : ActionFilterAttribute
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// OnActionExecuted是在Action中的代码执行之后运行的方法。
        /// </summary>
        /// <param name="context"></param>
        public override void OnResultExecuted(ResultExecutedContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor) return;

            //获得注解信息
            LogAttribute logAttribute = GetLogAttribute(controllerActionDescriptor);
            if (logAttribute == null) return;

            try
            {
                string method = context.HttpContext.Request.Method.ToUpper();
                // 获取当前的用户
                string userName = context.HttpContext.GetName();
                string jsonResult = string.Empty;

                if (context.Result is ContentResult result && result.ContentType == "application/json")
                {
                    jsonResult = result.Content.Replace("\r\n", "").Trim();
                }
                //获取当前执行方法的类名
                //string className =  System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name;
                //获取当前成员的名称
                //string methodName = System.Reflection.MethodBase.GetCurrentMethod().Name;
                string controller = context.RouteData.Values["Controller"].ToString();
                string action = context.RouteData.Values["Action"].ToString();

                string ip = HttpContextExtension.GetClientUserIp(context.HttpContext);
                var ip_info = IpTool.Search(ip);

                SysOperLog sysOperLog = new SysOperLog
                {
                    status = 0,
                    operName = userName,
                    operIp = ip,
                    operUrl = HttpContextExtension.GetRequestUrl(context.HttpContext),
                    requestMethod = method,
                    jsonResult = jsonResult,
                    operLocation = ip_info.Province + " " + ip_info.City,
                    method = controller + "." + action + "()",
                    //Elapsed = _stopwatch.ElapsedMilliseconds,
                    operTime = DateTime.Now
                };
                GetRequestValue(sysOperLog, context.HttpContext);

                if (logAttribute != null)
                {
                    sysOperLog.title = logAttribute?.Title;
                    sysOperLog.businessType = (int)logAttribute?.BusinessType;
                    sysOperLog.operParam = logAttribute.IsSaveRequestData ? sysOperLog.operParam : "";
                    sysOperLog.jsonResult = logAttribute.IsSaveResponseData ? sysOperLog.jsonResult : "";
                }
                var sysOperLogService = (ISysOperLogService)App.GetRequiredService(typeof(ISysOperLogService));

                sysOperLogService.InsertOperlog(sysOperLog);

                LogEventInfo ei = new(LogLevel.Info, "GlobalExceptionMiddleware", "");

                ei.Properties["status"] = 0;
                ei.Properties["jsonResult"] = !HttpMethods.IsGet(method) ? jsonResult : "";
                ei.Properties["requestParam"] = sysOperLog.operParam;
                ei.Properties["user"] = userName;

                logger.Log(ei);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"记录操作日志出错了#{ex.Message}");
            }
        }

        private LogAttribute GetLogAttribute(ControllerActionDescriptor controllerActionDescriptor)
        {
            var attribute = controllerActionDescriptor.MethodInfo.GetCustomAttributes(inherit: true)
                .FirstOrDefault(a => a.GetType().Equals(typeof(LogAttribute)));

            return (LogAttribute)attribute;
        }

        /// <summary>
        /// 设置请求参数
        /// </summary>
        /// <param name="operLog"></param>
        /// <param name="context"></param>
        public static void GetRequestValue(SysOperLog operLog, HttpContext context)
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