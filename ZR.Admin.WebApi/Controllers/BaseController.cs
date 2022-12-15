using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using ff = System.IO;

namespace ZR.Admin.WebApi.Controllers
{
    public class BaseController : ControllerBase
    {
        public static string TIME_FORMAT_FULL = "yyyy-MM-dd HH:mm:ss";
        public static string TIME_FORMAT_FULL_2 = "MM-dd HH:mm:ss";

        /// <summary>
        /// 返回成功封装
        /// </summary>
        /// <param name="data"></param>
        /// <param name="timeFormatStr"></param>
        /// <returns></returns>
        protected IActionResult SUCCESS(object data, string timeFormatStr = "yyyy-MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(GetApiResult(data != null ? ResultCode.SUCCESS : ResultCode.FAIL, data), timeFormatStr);
            return Content(jsonStr, "application/json");
        }

        /// <summary>
        /// json输出带时间格式的
        /// </summary>
        /// <param name="apiResult"></param>
        /// <param name="timeFormatStr"></param>
        /// <returns></returns>
        protected IActionResult ToResponse(ApiResult apiResult, string timeFormatStr = "yyyy-MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(apiResult, timeFormatStr);

            return Content(jsonStr, "application/json");
        }

        protected IActionResult ToResponse(long rows, string timeFormatStr = "yyyy-MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(ToJson(rows), timeFormatStr);

            return Content(jsonStr, "application/json");
        }

        protected IActionResult ToResponse(ResultCode resultCode, string msg = "")
        {
            return ToResponse(GetApiResult(resultCode, msg));
        }

        /// <summary>
        /// 导出Excel
        /// </summary>
        /// <param name="path">完整文件路径</param>
        /// <param name="fileName">带扩展文件名</param>
        /// <returns></returns>
        protected IActionResult ExportExcel(string path, string fileName)
        {
            //IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            //string fileDir = Path.Combine(webHostEnvironment.WebRootPath, path, fileName);

            var stream = ff.File.OpenRead(path);  //创建文件流
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", HttpUtility.UrlEncode(fileName));
        }
        
        #region 方法

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
        protected ApiResult GetApiResult(ResultCode resultCode, object? data = null)
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
        #endregion

        /// <summary>
        /// 导出Excel
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="sheetName"></param>
        /// <param name="fileName"></param>
        protected string ExportExcel<T>(List<T> list, string sheetName, string fileName)
        {
            return ExportExcelMini(list, sheetName, fileName).Item1;
        }

        protected (string, string) ExportExcelMini<T>(List<T> list, string sheetName, string fileName)
        {
            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}{DateTime.Now:MM-dd-HHmmss}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "export", sFileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            MiniExcel.SaveAs(fullPath, list, sheetName: sheetName);
            return (sFileName, fullPath);
        }

        /// <summary>
        /// 导出多个工作表(Sheet)
        /// </summary>
        /// <param name="sheets"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        protected (string, string) ExportExcelMini(Dictionary<string, object> sheets, string fileName)
        {
            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}{DateTime.Now:MM-dd-HHmmss}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "export", sFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            MiniExcel.SaveAs(fullPath, sheets);
            return (sFileName, fullPath);
        }

        /// <summary>
        /// 下载导入模板
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="stream"></param>
        /// <param name="fileName">下载文件名</param>
        /// <returns></returns>
        protected string DownloadImportTemplate<T>(List<T> list, Stream stream, string fileName)
        {
            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}模板.xlsx";
            string newFileName = Path.Combine(webHostEnvironment.WebRootPath, "importTemplate", sFileName);
            
            if (!Directory.Exists(newFileName))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newFileName));
            }
            MiniExcel.SaveAs(newFileName, list);
            return sFileName;
        }
    }
}
