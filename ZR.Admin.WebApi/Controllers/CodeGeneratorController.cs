using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.CodeGenerator;
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
        public ICodeGeneratorService CodeGeneratorService;
        public CodeGeneratorController(ICodeGeneratorService codeGeneratorService)
        {
            CodeGeneratorService = codeGeneratorService;
        }

        /// <summary>
        /// 获取所有数据库的信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetListDataBase")]
        //[YuebonAuthorize("GetListDataBase")]
        //[NoPermissionRequired]
        public IActionResult GetListDataBase()
        {
            List<DataBaseInfo> listTable = CodeGeneratorService.GetAllDataBases("SqlServer");

            return SUCCESS(listTable);
        }

        /// <summary>
        ///获取所有表根据数据名
        /// </summary>
        /// <param name="enCode">数据库名</param>
        /// <param name="keywords">表名</param>
        /// <param name="pagerInfo">分页信息</param>
        /// <returns></returns>
        [HttpGet("FindListTable")]
        public IActionResult FindListTable(string enCode, string keywords, PagerInfo pagerInfo)
        {
            if (string.IsNullOrEmpty(enCode))
            {
                return ToRespose(ResultCode.PARAM_ERROR);
            }
            List<DbTableInfo> listTable = CodeGeneratorService.GetTablesWithPage(keywords, enCode, pagerInfo);

            return SUCCESS(listTable);
        }
    }
}
