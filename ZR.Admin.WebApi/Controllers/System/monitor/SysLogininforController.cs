using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    /// <summary>
    /// 系统访问记录
    /// </summary>
    [Verify]
    [Route("/monitor/logininfor")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysLogininforController : BaseController
    {
        private ISysLoginService sysLoginService;

        public SysLogininforController(ISysLoginService sysLoginService)
        {
            this.sysLoginService = sysLoginService;
        }

        /// <summary>
        /// 查询登录日志
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "monitor:logininfor:list")]
        public IActionResult LoignLogList([FromQuery] SysLogininfoQueryDto param)
        {
            var list = sysLoginService.GetLoginLog(param);

            return SUCCESS(list);
        }
        
        /// <summary>
        /// 查询我的登录日志
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        [HttpGet("mylist")]
        public IActionResult QueryMyLoignLogList([FromQuery] SysLogininfoQueryDto param)
        {
            param.UserId = HttpContext.GetUId();
            var list = sysLoginService.GetLoginLog(param);

            return SUCCESS(list);
        }

        /// <summary>
        /// 清空登录日志
        /// </summary>
        /// <returns></returns>
        [Log(Title = "清空登录日志", BusinessType = BusinessType.CLEAN)]
        [ActionPermissionFilter(Permission = "monitor:logininfor:remove")]
        [HttpDelete("clean")]
        public IActionResult CleanLoginInfo()
        {
            if (!HttpContextExtension.IsAdmin(HttpContext))
            {
                return ToResponse(ApiResult.Error("操作失败"));
            }
            sysLoginService.TruncateLogininfo();
            return SUCCESS(1);
        }

        /// <summary>
        /// </summary>
        /// <param name="infoIds"></param>
        /// <returns></returns>
        [Log(Title = "删除登录日志", BusinessType = BusinessType.DELETE)]
        [HttpDelete("{infoIds}")]
        [ActionPermissionFilter(Permission = "monitor:logininfor:remove")]
        public IActionResult Remove(string infoIds)
        {
            if (!HttpContextExtension.IsAdmin(HttpContext))
            {
                return ToResponse(ApiResult.Error("操作失败"));
            }
            long[] infoIdss = Tools.SpitLongArrary(infoIds);
            return SUCCESS(sysLoginService.DeleteLogininforByIds(infoIdss));
        }

        /// <summary>
        /// 登录日志导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "登录日志导出")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "monitor:logininfor:export")]
        public IActionResult Export([FromQuery] SysLogininfor logininfoDto)
        {
            logininfoDto.BeginTime = DateTimeHelper.GetBeginTime(logininfoDto.BeginTime, -1);
            logininfoDto.EndTime = DateTimeHelper.GetBeginTime(logininfoDto.EndTime, 1);
            var exp = Expressionable.Create<SysLogininfor>()
                .And(it => it.LoginTime >= logininfoDto.BeginTime && it.LoginTime <= logininfoDto.EndTime);

            var list = sysLoginService.Queryable().Where(exp.ToExpression())
                .ToList();

            string sFileName = ExportExcel(list, "loginlog", "登录日志");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }

        /// <summary>
        /// 查询登录日志统计
        /// </summary>
        /// <returns></returns>
        [HttpGet("statiLoginLog")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult QueryStatiLoginLog()
        {
            var list = sysLoginService.GetStatiLoginlog();
            var categories = list.Select(x => x.Date.ToString("dd日")).ToList();
            var numList = list.Select(x => x.Num).ToList();
            return SUCCESS(new { categories, numList });
        }
    }
}
