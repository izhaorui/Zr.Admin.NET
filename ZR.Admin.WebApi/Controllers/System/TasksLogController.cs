using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using ZR.Model;
using ZR.Model.Dto.System;
using ZR.Model.System;
using Infrastructure.Extensions;
using Infrastructure.Attribute;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using Infrastructure.Enums;
using ZR.Service.IService;

namespace ZR.Admin.WebApi.Controllers.System
{
    [Route("/monitor/jobLog")]
    public class TasksLogController : BaseController
    {
        private readonly ISysTasksLogService tasksLogService;

        public TasksLogController(ISysTasksLogService tasksLogService)
        {
            this.tasksLogService = tasksLogService;
        }

        /// <summary>
        /// 查询日志
        /// </summary>
        /// <param name="queryDto"></param>
        /// <param name="pager"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult GetList([FromQuery] PagerInfo pager, [FromQuery] TasksLogQueryDto queryDto)
        {
            //开始拼装查询条件
            queryDto.BeginTime = DateTimeHelper.GetBeginTime(queryDto.BeginTime, -1);
            queryDto.EndTime = DateTimeHelper.GetBeginTime(queryDto.EndTime, 1);

            var predicate = Expressionable.Create<SysTasksLog>().And(it => it.CreateTime >= queryDto.BeginTime && it.CreateTime <= queryDto.EndTime);
            predicate = predicate.AndIF(queryDto.JobName.IfNotEmpty(), m => m.JobName.Contains(queryDto.JobName));
            predicate = predicate.AndIF(queryDto.JobGroup.IfNotEmpty(), m => m.JobGroup == queryDto.JobGroup);
            predicate = predicate.AndIF(queryDto.Status.IfNotEmpty(), m => m.Status == queryDto.Status);
            predicate = predicate.AndIF(queryDto.JobId.IfNotEmpty(), m => m.JobId == queryDto.JobId);

            var response = tasksLogService.GetPages(predicate.ToExpression(), pager, m => m.CreateTime, "Desc");

            return SUCCESS(response, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 删除定时任务调用日志
        /// </summary>
        /// <param name="jobIds"></param>
        /// <returns></returns>
        [HttpDelete("{jobIds}")]
        [ActionPermissionFilter(Permission = "PRIV_JOBLOG_DELETE")]
        [Log(Title = "删除任务日志", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(string jobIds)
        {
            long[] jobIdArr = Tools.SpitLongArrary(jobIds);

            int result = tasksLogService.Delete(jobIdArr);

            return OutputJson(ToJson(result, result));
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        /// <returns></returns>
        [HttpDelete("clean")]
        [ActionPermissionFilter(Permission = "PRIV_JOBLOG_REMOVE")]
        [Log(Title = "清空任务日志", BusinessType = BusinessType.CLEAN)]
        public IActionResult Clean()
        {
            tasksLogService.DeleteTable();
            return SUCCESS(1);
        }
    }
}
