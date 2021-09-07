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
using ZR.CodeGenerator.Service;
using ZR.Model;
using ZR.Model.CodeGenerator;
using ZR.Model.Vo;
using ZR.Service.IService;
using ZR.Service.System;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 代码生成
    /// </summary>
    [Route("codeGenerator")]
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
        [HttpGet("GetListDataBase")]
        //[YuebonAuthorize("GetListDataBase")]
        //[NoPermissionRequired]
        public IActionResult GetListDataBase()
        {
            return SUCCESS(_CodeGeneraterService.GetAllDataBases());
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
            if (string.IsNullOrEmpty(dbName))
            {
                dbName = "ZrAdmin";
            }
            List<SqlSugar.DbTableInfo> list = _CodeGeneraterService.GetAllTables(dbName, tableName, pager);
            var vm = new VMPageResult<SqlSugar.DbTableInfo>(list, pager);

            return SUCCESS(vm);
        }

        /// <summary>
        /// 代码生成器
        /// </summary>
        /// <param name="dbName"></param>
        /// <param name="tables">要生成代码的表</param>
        /// <param name="baseSpace">项目命名空间</param>
        /// <param name="replaceTableNameStr">要删除表名的字符串用英文逗号","隔开</param>
        /// <returns></returns>
        [HttpGet("Generate")]
        [Log(Title = "代码生成", BusinessType = BusinessType.OTHER)]
        public IActionResult Generate(string dbName, string baseSpace, string tables, string replaceTableNameStr)
        {
            if (string.IsNullOrEmpty(tables))
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请求参数为空");
            }
            if (string.IsNullOrEmpty(baseSpace))
            {
                //baseSpace = "Zr";
            }
            DbTableInfo dbTableInfo = new() { Name = tables };
            CodeGeneratorTool.Generate(dbName, baseSpace, dbTableInfo, replaceTableNameStr, true);

            return SUCCESS(1);
        }
    }
}
