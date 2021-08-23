using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NLog;
using System;

namespace ZR.Admin.WebApi.Filters
{
    public class GlobalActionMonitor : Attribute, IActionFilter
    {
        static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// OnActionExecuting是在Action执行之前运行的方法。
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid) return;
            ApiResult response = new ApiResult();
            response.Code = (int)ResultCode.PARAM_ERROR;

            //var keys = context.ModelState.Keys;
            var values = context.ModelState.Values;
            foreach (var item in values)
            {
                foreach (var err in item.Errors)
                {
                    if (err.ErrorMessage.Contains("JSON"))
                    {
                        return;
                    }
                    if (!string.IsNullOrEmpty(response.Msg))
                    {
                        response.Msg += " | ";
                    }

                    response.Msg += err.ErrorMessage;
                }
            }
            logger.Info($"请求参数错误,{response.Msg}");
            context.Result = new JsonResult(response);
        }

        /// <summary>
        /// OnActionExecuted是在Action中的代码执行之后运行的方法。
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
