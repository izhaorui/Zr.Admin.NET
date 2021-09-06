using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ZR.Admin.WebApi.Filters;

namespace ZR.Admin.WebApi.Controllers
{
    [LogActionFilter]
    public class BaseController : ControllerBase
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static string TIME_FORMAT_FULL = "yyyy-MM-dd HH:mm:ss";
        public static string TIME_FORMAT_FULL_2 = "MM-dd HH:mm:ss";

        protected IActionResult SUCCESS(object data, string timeFormatStr = "MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(GetApiResult(data != null ? ResultCode.SUCCESS : ResultCode.FAIL, data), timeFormatStr);
            return Content(jsonStr, "application/json");
        }

        protected IActionResult ToRespose(ResultCode resultCode, object data = null)
        {
            string jsonStr = GetJsonStr(GetApiResult(resultCode, data), "");
            return Content(jsonStr, "application/json");
        }

        /// <summary>
        /// json输出带时间格式的
        /// </summary>
        /// <param name="apiResult"></param>
        /// <param name="timeFormatStr"></param>
        /// <returns></returns>
        protected IActionResult OutputJson(ApiResult apiResult, string timeFormatStr = "MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(apiResult, timeFormatStr);

            return Content(jsonStr, "application/json");
        }
        protected IActionResult OutputJson(long rows, string timeFormatStr = "MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(ToJson(rows), timeFormatStr);

            return Content(jsonStr, "application/json");
        }

        /// <summary>
        /// 响应返回结果
        /// </summary>
        /// <param name="rows">受影响行数</param>
        /// <returns></returns>
        protected ApiResult ToJson(long rows)
        {
            return rows > 0 ? GetApiResult(ResultCode.SUCCESS) : GetApiResult(ResultCode.FAIL);
        }
        protected ApiResult ToJson(long rows, object data)
        {
            return rows > 0 ? GetApiResult(ResultCode.SUCCESS, data) : GetApiResult(ResultCode.FAIL);
        }
        /// <summary>
        /// 全局Code使用
        /// </summary>
        /// <param name="resultCode"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected ApiResult GetApiResult(ResultCode resultCode, object data = null)
        {
            var apiResult = new ApiResult((int)resultCode, resultCode.ToString())
            {
                Data = data
            };

            return apiResult;
        }
        protected ApiResult GetApiResult(ResultCode resultCode, string msg)
        {
            return new ApiResult((int)resultCode, msg);
        }
        private static string GetJsonStr(ApiResult apiResult, string timeFormatStr)
        {
            if (string.IsNullOrEmpty(timeFormatStr))
            {
                timeFormatStr = TIME_FORMAT_FULL;
            }
            var serializerSettings = new JsonSerializerSettings
            {
                // 设置为驼峰命名
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateFormatString = timeFormatStr
            };

            return JsonConvert.SerializeObject(apiResult, Formatting.Indented, serializerSettings);
        }

        protected IActionResult CustomError(ResultCode resultCode, string msg = "")
        {
            return OutputJson(GetApiResult(resultCode, msg));
        }
    }
}
