using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System.Dto;


//创建时间：2023-08-17
namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 数据差异日志
    /// </summary>
    [Verify]
    [Route("monitor/SqlDiffLog")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SqlDiffLogController : BaseController
    {
        /// <summary>
        /// 数据差异日志接口
        /// </summary>
        private readonly ISqlDiffLogService _SqlDiffLogService;

        public SqlDiffLogController(ISqlDiffLogService SqlDiffLogService)
        {
            _SqlDiffLogService = SqlDiffLogService;
        }

        /// <summary>
        /// 查询数据差异日志列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "sqldifflog:list")]
        public IActionResult QuerySqlDiffLog([FromQuery] SqlDiffLogQueryDto parm)
        {
            var response = _SqlDiffLogService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 删除数据差异日志
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "sqldifflog:delete")]
        [Log(Title = "数据差异日志", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteSqlDiffLog(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _SqlDiffLogService.Delete(idsArr);

            return ToResponse(response);
        }

        /// <summary>
        /// 导出数据差异日志
        /// </summary>
        /// <returns></returns>
        [Log(Title = "数据差异日志", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "sqldifflog:export")]
        public IActionResult Export([FromQuery] SqlDiffLogQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _SqlDiffLogService.GetList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "数据差异日志", "数据差异日志");
            return ExportExcel(result.Item2, result.Item1);
        }
    }
}