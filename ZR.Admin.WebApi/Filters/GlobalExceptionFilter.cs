using System;
using System.Diagnostics;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ZRAdmin.Filters
{
    /// <summary>
    /// 全局异常捕获
    /// 暂时没用了，改为中间件 2021-2-26
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IWebHostEnvironment _hostingEnvironment;

        public GlobalExceptionFilter(IWebHostEnvironment environment)
        {
            _hostingEnvironment = environment;
        }

        public void OnException(ExceptionContext context)
        {
            Exception ex = context.Exception;

            int code = (int)ResultCode.GLOBAL_ERROR;
            string msg;
            //自定义异常
            if (ex is CustomException customException)
            {
                code = customException.Code;
                msg = customException.Message;

                logger.Info($"自定义异常code={code},msg={msg}");
            }
            else
            //参数异常
            if (ex is ArgumentException)
            {
                code = (int)ResultCode.PARAM_ERROR;
                msg = ex.Message;
            }
            else
            {
                msg = "服务器好像出了点问题......";
                Console.WriteLine(ex.StackTrace);
                // 获取异常堆栈
                var traceFrame = new StackTrace(ex, true).GetFrame(0);
                // 获取出错的文件名
                var exceptionFileName = traceFrame.GetFileName();
                // 获取出错的行号
                var exceptionFileLineNumber = traceFrame.GetFileLineNumber();
                var errorMsg = $"{ex.Message}##{traceFrame}##{exceptionFileName}##行号:{exceptionFileLineNumber}";

                logger.Error(ex, $"系统出错了error={ex.Message}#{errorMsg}");
                //context.Response.StatusCode = 500;
            }

            context.Result = new JsonResult(new { code, msg });
            context.ExceptionHandled = true;
        }

    }
}