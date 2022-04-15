using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;

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
            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}{DateTime.Now:MMddHHmmss}.xlsx";
            string newFileName = Path.Combine(webHostEnvironment.WebRootPath, "export", sFileName);
            //调试模式需要加上
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            Directory.CreateDirectory(Path.GetDirectoryName(newFileName));
            using (ExcelPackage package = new(new FileInfo(newFileName)))
            {
                // 添加worksheet
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(sheetName);
                //单元格自动适应大小
                worksheet.Cells.Style.ShrinkToFit = true;
                //全部字段导出
                worksheet.Cells.LoadFromCollection(list, true, OfficeOpenXml.Table.TableStyles.Light13);
                package.Save();
            }

            return sFileName;
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
            //调试模式需要加上
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            if (!Directory.Exists(newFileName))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newFileName));
            }
            using (ExcelPackage package = new(new FileInfo(newFileName)))
            {
                // 添加worksheet
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(fileName);
                //单元格自动适应大小
                worksheet.Cells.Style.ShrinkToFit = true;
                //全部字段导出
                worksheet.Cells.LoadFromCollection(list, true, OfficeOpenXml.Table.TableStyles.Light13);
                package.SaveAs(stream);
            }

            return sFileName;
        }
    }
}
