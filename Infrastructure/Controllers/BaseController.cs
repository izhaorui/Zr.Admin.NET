using Infrastructure.Extensions;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", HttpUtility.UrlEncode(fileName));
        }

        /// <summary>
        /// 异步导出Excel（使用异步文件流）
        /// </summary>
        /// <param name="path">完整文件路径</param>
        /// <param name="fileName">带扩展文件名</param>
        /// <returns></returns>
        protected async Task<IActionResult> ExportExcelAsync(string path, string fileName)
        {
            if (!Path.Exists(path))
            {
                throw new CustomException(fileName + "文件不存在");
            }
            // 使用异步文件流打开文件，允许异步IO
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            // 确保方法为真正的异步方法，避免编译器警告
            await Task.Yield();

            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
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
                return ToResponse(ResultCode.CUSTOM_ERROR, "文件不存在");
            }
            var stream = System.IO.File.OpenRead(path);  //创建文件流
            Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
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
                // 默认驼峰命名；公网 C 端宿主换成 FrontContractResolver 移除审计字段。
                // 契约解析器走 App.ResponseContractResolver 而不是在这里写死，也不依赖宿主的
                // AddNewtonsoftJson —— 本方法自建 settings 序列化，MVC 的格式化器完全不参与。
                ContractResolver = App.ResponseContractResolver,
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
            string sFileName = $"{fileName}_{DateTime.Now:MMdd_HHmmss}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "export", sFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            MiniExcel.SaveAs(fullPath, list, sheetName: sheetName);
            return (sFileName, fullPath);
        }
        protected async Task<(string, string)> ExportExcelMiniAsync<T>(List<T> list, string sheetName, string fileName)
        {
            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}_{DateTime.Now:MMdd_HHmmss}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "export", sFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            await MiniExcel.SaveAsAsync(fullPath, list, sheetName: sheetName);
            return (sFileName, fullPath);
        }

        /// <summary>
        /// 导出「列由运行时决定」的图表数据到 Excel（AI 聚合结果、销售趋势/排名等）。
        ///
        /// 与 <see cref="ExportExcelMini{T}"/> 的区别：这些数据的字段在编译期不确定
        /// （字典行 + 运行时列定义），无法用带 <c>ExcelColumn</c> 的强类型承载，
        /// 故用 <see cref="DataTable"/> 保留列顺序与中文表头。
        /// </summary>
        /// <param name="rows">数据行；每行的 key 为字段名</param>
        /// <param name="columns">列定义：Field=取值字段名，Label=表头显示名；为空时按首行键推断（表头用字段名）</param>
        /// <param name="sheetName">工作表名</param>
        /// <param name="fileName">文件名（不含扩展名与时间戳）</param>
        protected (string, string) ExportChartExcel(
            IEnumerable<IDictionary<string, object>> rows,
            IList<(string Field, string Label)> columns,
            string sheetName,
            string fileName)
        {
            var rowList = rows?.ToList() ?? new List<IDictionary<string, object>>();
            var cols = columns?.Where(c => !string.IsNullOrWhiteSpace(c.Field)).ToList() ?? new List<(string Field, string Label)>();

            // 无显式列定义时按首行键推断，至少能导出内容；空数据会得到一个只有表头的文件。
            if (cols.Count == 0)
            {
                var first = rowList.FirstOrDefault();
                if (first != null)
                {
                    cols = first.Keys.Select(k => (k, k)).ToList();
                }
            }

            var table = new DataTable();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columnNames = new List<string>(cols.Count);
            for (var i = 0; i < cols.Count; i++)
            {
                // DataTable 列名必须唯一：优先用中文表头，重名时回退字段名并加序号。
                var name = string.IsNullOrWhiteSpace(cols[i].Label) ? cols[i].Field : cols[i].Label.Trim();
                if (!used.Add(name))
                {
                    name = cols[i].Field;
                    var n = 1;
                    while (!used.Add(name))
                    {
                        name = $"{cols[i].Field}_{n++}";
                    }
                }
                table.Columns.Add(name, typeof(object));
                columnNames.Add(name);
            }

            foreach (var row in rowList)
            {
                var dr = table.NewRow();
                for (var i = 0; i < cols.Count; i++)
                {
                    dr[columnNames[i]] = row != null && row.TryGetValue(cols[i].Field, out var v)
                        ? NormalizeCellValue(v)
                        : string.Empty;
                }
                table.Rows.Add(dr);
            }

            IWebHostEnvironment webHostEnvironment = (IWebHostEnvironment)App.ServiceProvider.GetService(typeof(IWebHostEnvironment));
            string sFileName = $"{fileName}_{DateTime.Now:MMdd_HHmmss}.xlsx";
            string fullPath = Path.Combine(webHostEnvironment.WebRootPath, "export", sFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            MiniExcel.SaveAs(fullPath, table, sheetName: sheetName);
            return (sFileName, fullPath);
        }

        /// <summary>
        /// 把图表单元格值归一化为可写入 Excel 的基础类型。
        ///
        /// 请求体经 System.Text.Json 反序列化后，<c>Dictionary&lt;string, object&gt;</c> 的值是
        /// <see cref="System.Text.Json.JsonElement"/>；直接写入单元格只会落成原始 JSON 文本
        /// （字符串带引号、数字/布尔变字面量）。Newtonsoft 的 JToken 同理，一并处理。
        /// </summary>
        private static object NormalizeCellValue(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case System.Text.Json.JsonElement el:
                    return JsonElementToCell(el);
                case Newtonsoft.Json.Linq.JToken jt:
                    return JTokenToCell(jt);
                default:
                    return value;
            }
        }

        private static object JsonElementToCell(System.Text.Json.JsonElement el)
        {
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    return el.GetString();
                case System.Text.Json.JsonValueKind.Number:
                    if (el.TryGetInt64(out var l)) return l;
                    if (el.TryGetDecimal(out var d)) return d;
                    return el.GetDouble();
                case System.Text.Json.JsonValueKind.True:
                case System.Text.Json.JsonValueKind.False:
                    return el.GetBoolean();
                case System.Text.Json.JsonValueKind.Null:
                case System.Text.Json.JsonValueKind.Undefined:
                    return string.Empty;
                default:
                    // 对象/数组：保留紧凑 JSON 文本
                    return el.GetRawText();
            }
        }

        private static object JTokenToCell(Newtonsoft.Json.Linq.JToken jt)
        {
            if (jt is Newtonsoft.Json.Linq.JValue jv)
            {
                return jv.Value ?? string.Empty;
            }
            return jt.ToString(Formatting.None);
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
