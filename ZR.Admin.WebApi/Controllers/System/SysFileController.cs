using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model.System;
using ZR.Service.System.IService;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 文件存储Controller
    /// </summary>
    [Verify]
    [Route("tool/file")]
    public class SysFileController : BaseController
    {
        /// <summary>
        /// 文件存储接口
        /// </summary>
        private readonly ISysFileService _SysFileService;

        public SysFileController(ISysFileService SysFileService)
        {
            _SysFileService = SysFileService;
        }

        /// <summary>
        /// 查询文件存储列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "tool:file:list")]
        public IActionResult QuerySysFile([FromQuery] SysFileQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<SysFile>();
            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.AndIF(parm.BeginCreate_time != null, it => it.Create_time >= parm.BeginCreate_time);
            predicate = predicate.AndIF(parm.EndCreate_time != null, it => it.Create_time <= parm.EndCreate_time);
            predicate = predicate.AndIF(parm.StoreType != null, m => m.StoreType == parm.StoreType);
            predicate = predicate.AndIF(parm.FileId != null, m => m.Id == parm.FileId);

            //搜索条件查询语法参考Sqlsugar
            var response = _SysFileService.GetPages(predicate.ToExpression(), parm, x => x.Id, OrderByType.Desc);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询文件存储详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "tool:file:query")]
        public IActionResult GetSysFile(long Id)
        {
            var response = _SysFileService.GetFirst(x => x.Id == Id);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除文件存储
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "tool:file:delete")]
        [Log(Title = "文件存储", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteSysFile(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _SysFileService.Delete(idsArr);
            //TODO 删除本地资源

            return ToResponse(response);
        }

        /// <summary>
        /// 文件存储导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "文件存储")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "tool:file:export")]
        public IActionResult Export()
        {
            var list = _SysFileService.GetAll();

            string sFileName = ExportExcel(list, "SysFile", "文件存储");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }
    }
}