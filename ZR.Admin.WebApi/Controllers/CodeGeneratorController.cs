using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Collections.Generic;
using ZR.Admin.WebApi.Filters;
using ZR.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Model;
using ZR.Model.Vo;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Route("tool/gen")]
    public class CodeGeneratorController : BaseController
    {
        private CodeGeneraterService _CodeGeneraterService = new CodeGeneraterService();

        /// <summary>
        /// 获取所有数据库的信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getDbList")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult GetListDataBase()
        {
            var dbList = _CodeGeneraterService.GetAllDataBases();
            var defaultDb = dbList.Count > 0 ? dbList[0] : null;
            return SUCCESS(new { dbList, defaultDb });
        }

        /// <summary>
        ///获取所有表根据数据名
        /// </summary>
        /// <param name="dbName">数据库名</param>
        /// <param name="tableName">表名</param>
        /// <param name="pager">分页信息</param>
        /// <returns></returns>
        [HttpGet("getTableList")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult FindListTable(string dbName, string tableName, PagerInfo pager)
        {
            List<DbTableInfo> list = _CodeGeneraterService.GetAllTables(dbName, tableName, pager);
            var vm = new VMPageResult<DbTableInfo>(list, pager);

            return SUCCESS(vm);
        }

        /// <summary>
        /// 获取表格列
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        [HttpGet("getColumnInfo")]
        [ActionPermissionFilter(Permission = "tool:gen:list")]
        public IActionResult QueryColumnInfo(string dbName, string tableName)
        {
            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(tableName))
                return ToRespose(ResultCode.PARAM_ERROR);

            return SUCCESS(_CodeGeneraterService.GetColumnInfo(dbName, tableName));
        }

        /// <summary>
        /// 代码生成器
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        /// <returns></returns>
        [HttpPost("genCode")]
        [Log(Title = "代码生成", BusinessType = BusinessType.GENCODE)]
        [ActionPermissionFilter(Permission = "tool:gen:code")]
        public IActionResult Generate([FromBody] GenerateDto dto)
        {
            if (string.IsNullOrEmpty(dto.tableName))
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            DbTableInfo dbTableInfo = new() { Name = dto.tableName };
            CodeGeneratorTool.Generate(dbTableInfo, dto);

            return SUCCESS(dbTableInfo);
        }
    }
}
