﻿using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Model.Vo;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    [Verify]
    [Route("/monitor/operlog")]
    public class SysOperlogController : BaseController
    {
        private ISysOperLogService sysOperLogService;

        public SysOperlogController(ISysOperLogService sysOperLogService)
        {
            this.sysOperLogService = sysOperLogService;
        }

        /// <summary>
        /// 查询操作日志
        /// </summary>
        /// <param name="sysOperLog"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult OperList([FromQuery] SysOperLogDto sysOperLog)
        {
            PagerInfo pagerInfo = new PagerInfo(sysOperLog.pageNum);

            var list = sysOperLogService.SelectOperLogList(sysOperLog, pagerInfo);
            var vMPage = new VMPageResult<SysOperLog>(list, pagerInfo);

            return OutputJson(ToJson(vMPage.TotalNum, vMPage), TIME_FORMAT_FULL_2);
        }

        /// <summary>
        /// 删除操作日志
        /// </summary>
        /// <param name="operIds"></param>
        /// <returns></returns>
        [Log(Title = "操作日志", BusinessType = BusinessType.DELETE)]
        [ActionPermissionFilter(Permission = "monitor:operlog:delete")]
        [HttpDelete("{operIds}")]
        public IActionResult Remove(string operIds)
        {
            long[] operIdss = Tools.SpitLongArrary(operIds);
            return SUCCESS(sysOperLogService.DeleteOperLogByIds(operIdss));
        }

        /// <summary>
        /// 清空操作日志
        /// </summary>
        /// <returns></returns>
        [Log(Title = "清空操作日志", BusinessType = BusinessType.CLEAN)]
        [ActionPermissionFilter(Permission = "monitor:operlog:delete")]
        [HttpDelete("clean")]
        public ApiResult ClearOperLog()
        {
            sysOperLogService.CleanOperLog();

            return ToJson(1);
        }

    }
}
