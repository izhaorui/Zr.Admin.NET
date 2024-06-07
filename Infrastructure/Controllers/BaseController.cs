using Infrastructure.Extensions;
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

namespace Infrastructure.Controllers
{
    /// <summary>
    /// web层通用数据处理
    /// </summary>
    //[ApiController]
    public class BaseController : ControllerBase
    {
        public static string TIME_FORMAT_FULL = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// 返回成功封装
        /// </summary>
        /// <param name="data"></param>
        /// <param name="timeFormatStr"></param>
        /// <returns></returns>
        protected IActionResult SUCCESS(object data, string timeFormatStr = "yyyy-MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(GetApiResult(data != null ? ResultCode.SUCCESS : ResultCode.NO_DATA, data), timeFormatStr);
            return Content(jsonStr, "application/json");
        }

        /// <summary>
        /// json输出带时间格式的
        /// </summary>
        /// <param name="apiResult"></param>
        /// <returns></returns>
        protected IActionResult ToResponse(ApiResult apiResult)
        {
            string jsonStr = GetJsonStr(apiResult, TIME_FORMAT_FULL);

            return Content(jsonStr, "application/json");
        }

        protected IActionResult ToResponse(long rows, string timeFormatStr = "yyyy-MM-dd HH:mm:ss")
        {
            string jsonStr = GetJsonStr(ToJson(rows), timeFormatStr);

            return Content(jsonStr, "application/json");
        }

        protected IActionResult ToResponse(ResultCode resultCode, string msg = "")
        {
            return ToResponse(new ApiResult((int)resultCode, msg));
        }

        /// <summary>
        /// 导出Excel
        /// </summary>
        /// <param name="path">完整文件路径</param>
        /// <param name="fileName">带扩展文件名</param>
        /// <returns></returns>
        protected IActionResult ExportExcel(string path, string fileName)
        {
            //var webHostEnvironment = App.WebHostEnvironment;
            if (!Path.Exists(path))
            {
                throw new CustomException(fileName + "文件不存在");
            }
            var stream = System.IO.File.OpenRead(path);  //创建文件流

            Response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition");
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", HttpUtility.UrlEncode(fileName));
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName">文件名，一定要带扩展名</param>
        /// <returns></returns>
        protected IActionResult DownFile(string path, string fileName)
        {
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }
            var stream = System.IO.File.OpenRead(path);  //创建文件流
            Response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition");
            return File(stream, "application/octet-stream", HttpUtility.UrlEncode(fileName));
        }

        #region 方法

        /// <summary>
        /// 响应返回结果
        /// </summary>
        /// <param name="rows">受影响行数</param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected ApiResult ToJson(long rows, object? data = null)
        {
            return rows > 0 ? ApiResult.Success("success", data) : GetApiResult(ResultCode.FAIL);
        }

        /// <summary>
        /// 全局Code使用
        /// </summary>
        /// <param name="resultCode"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected ApiResult GetApiResult(ResultCode resultCode, object? data = null)
        {
            var msg = resultCode.GetDescription();

            return new ApiResult((int)resultCode, msg, data);
        }
        protected ApiResult Success()
        {
            return GetApiResult(ResultCode.SUCCESS);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiResult"></param>
        /// <param name="timeFormatStr"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="sheetName"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
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
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="list">空数据类型集合</param>
        /// <param name="fileName">下载文件名</param>
        /// <returns></returns>
        protected (string, string) DownloadImportTemplate<T>(List<T> list, string fileName)
        {
            IWebHostEnvironment webHostEnvironment = App.WebHostEnvironment;
            string sFileName = $"{fileName}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "ImportTemplate", sFileName);

            //不存在模板创建模板
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            }
            if (!Path.Exists(fullPath))
            {
                MiniExcel.SaveAs(fullPath, list, overwriteFile: true);
            }
            return (sFileName, fullPath);
        }

        /// <summary>
        /// 下载指定文件模板
        /// </summary>
        /// <param name="fileName">下载文件名</param>
        /// <returns></returns>
        protected (string, string) DownloadImportTemplate(string fileName)
        {
            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "ImportTemplate", sFileName);

            return (sFileName, fullPath);
        }
    }
}
