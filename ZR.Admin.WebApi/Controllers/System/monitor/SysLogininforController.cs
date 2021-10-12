using Infrastructure.Attribute;
using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.Vo;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    /// <summary>
    /// 系统访问记录
    /// </summary>
    [Verify]
    [Route("/monitor/logininfor")]
    public class SysLogininforController : BaseController
    {
        private ISysLoginService sysLoginService;

        public SysLogininforController(ISysLoginService sysLoginService)
        {
            this.sysLoginService = sysLoginService;
        }

        /// <summary>
        /// 查询登录日志
        /// /monitor/logininfor/list
        /// </summary>
        /// <param name="sysLogininfoDto"></param>
        /// <param name="pagerInfo"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult LoignLogList([FromQuery] SysLogininfor sysLogininfoDto, [FromQuery] PagerInfo pagerInfo)
        {
            var list = sysLoginService.GetLoginLog(sysLogininfoDto, pagerInfo);
            var vMPage = new VMPageResult<SysLogininfor>(list, pagerInfo);

            return ToResponse(ToJson(vMPage.TotalNum, vMPage), TIME_FORMAT_FULL_2);
        }

        /// <summary>
        /// 清空登录日志
        /// /monitor/logininfor/clean
        /// </summary>
        /// <returns></returns>
        [Log(Title = "清空登录日志")]
        [ActionPermissionFilter(Permission = "monitor:logininfor:remove")]
        [HttpDelete("clean")]
        public IActionResult CleanLoginInfo()
        {
            sysLoginService.TruncateLogininfo();
            return SUCCESS(1);
        }

        /// <summary>
        /// /monitor/logininfor/1
        /// </summary>
        /// <param name="infoIds"></param>
        /// <returns></returns>
        [Log(Title = "删除登录日志")]
        [HttpDelete("{infoIds}")]
        [ActionPermissionFilter(Permission = "monitor:logininfor:remove")]
        public IActionResult Remove(string infoIds)
        {
            long[] infoIdss = Tools.SpitLongArrary(infoIds);
            return SUCCESS(sysLoginService.DeleteLogininforByIds(infoIdss));
        }

    }
}
