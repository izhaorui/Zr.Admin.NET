using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZR.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;
using ZR.Model;
using ZR.Model.Vo;
using ZR.Service.IService;
using ZR.Service.System;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Route("tool/gen")]
    public class CodeGeneratorController : BaseController
    {
        //public ICodeGeneratorService CodeGeneratorService;
        //public CodeGeneratorController(ICodeGeneratorService codeGeneratorService)
        //{
        //    CodeGeneratorService = codeGeneratorService;
        //}
        private CodeGeneraterService _CodeGeneraterService = new CodeGeneraterService();

        /// <summary>
        /// 获取所有数据库的信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("getDbList")]
        //[YuebonAuthorize("GetListDataBase")]
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
        [HttpGet("FindListTable")]
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
        [HttpGet("QueryColumnInfo")]
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
        [HttpGet("Generate")]
        [Log(Title = "代码生成", BusinessType = BusinessType.OTHER)]
        public IActionResult Generate([FromQuery] GenerateDto dto)
        {
            if (string.IsNullOrEmpty(dto.tables))
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            DbTableInfo dbTableInfo = new() { Name = dto.tables };
            CodeGeneratorTool.Generate(dbTableInfo, dto);

            return SUCCESS(1);
        }
    }
}
