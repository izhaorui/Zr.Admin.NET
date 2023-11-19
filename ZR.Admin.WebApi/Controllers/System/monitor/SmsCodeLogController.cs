using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.Dto;
using ZR.ServiceCore.Services;

//创建时间：2023-11-19
namespace ZR.Admin.WebApi.Controllers.System.monitor
{
    /// <summary>
    /// 短信验证码记录
    /// </summary>
    [Verify]
    [Route("system/SmscodeLog")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SmsCodeLogController : BaseController
    {
        /// <summary>
        /// 短信验证码记录接口
        /// </summary>
        private readonly ISmsCodeLogService _SmscodeLogService;

        public SmsCodeLogController(ISmsCodeLogService SmscodeLogService)
        {
            _SmscodeLogService = SmscodeLogService;
        }

        /// <summary>
        /// 查询短信验证码记录列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "smscodelog:list")]
        public IActionResult QuerySmscodeLog([FromQuery] SmscodeLogQueryDto parm)
        {
            var response = _SmscodeLogService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 删除短信验证码记录
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "smscodelog:delete")]
        [Log(Title = "短信验证码记录", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteSmscodeLog(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _SmscodeLogService.Delete(idsArr);

            return ToResponse(response);
        }

        /// <summary>
        /// 导出短信验证码记录
        /// </summary>
        /// <returns></returns>
        [Log(Title = "短信验证码记录", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "smscodelog:export")]
        public IActionResult Export([FromQuery] SmscodeLogQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _SmscodeLogService.GetList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "短信验证码记录", "短信验证码记录");
            return ExportExcel(result.Item2, result.Item1);
        }
    }
}