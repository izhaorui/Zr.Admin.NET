using Microsoft.AspNetCore.Mvc;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 日程管理Controller
    /// </summary>
    [Route("system/dailyschedule")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class DailyScheduleController : BaseController
    {
        private readonly IDailyScheduleService _DailyScheduleService;

        public DailyScheduleController(IDailyScheduleService DailyScheduleService)
        {
            _DailyScheduleService = DailyScheduleService;
        }

        /// <summary>
        /// 查询日程列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult QueryDailySchedule([FromQuery] DailyScheduleQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            var response = _DailyScheduleService.GetPages(parm, userId);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询统计（总/未完成/今日到期/已逾期）
        /// </summary>
        [HttpGet("stats")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult GetStats()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_DailyScheduleService.GetStats(userId));
        }

        /// <summary>
        /// 查询当前用户未完成日程列表（供消息中心日程 tab 打开时拉取，不写消息）
        /// </summary>
        [HttpGet("reminders")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult GetReminders()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_DailyScheduleService.GetReminderSchedules(userId));
        }

        /// <summary>
        /// 查询详情
        /// </summary>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult GetDailySchedule(long id)
        {
            var userId = HttpContext.GetUId();
            var response = _DailyScheduleService.GetById(id, userId);
            return SUCCESS(response);
        }

        /// <summary>
        /// 新增日程
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "日程新增", BusinessType = BusinessType.INSERT)]
        public IActionResult AddDailySchedule([FromBody] DailyScheduleDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            var userId = HttpContext.GetUId();
            var model = parm.Adapt<DailySchedule>().ToCreate(HttpContext);
            model.UserId = userId;
            return SUCCESS(_DailyScheduleService.AddDailySchedule(model));
        }

        /// <summary>
        /// 修改日程
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "日程修改", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateDailySchedule([FromBody] DailyScheduleDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求实体不能为空");
            }
            var userId = HttpContext.GetUId();
            var model = parm.Adapt<DailySchedule>().ToUpdate(HttpContext);
            model.UserId = userId;
            return SUCCESS(_DailyScheduleService.UpdateDailySchedule(model));
        }

        /// <summary>
        /// 标记完成 / 取消完成
        /// </summary>
        [HttpPut("changeStatus")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "日程状态切换", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeStatus([FromBody] DailyScheduleStatusDto parm)
        {
            if (parm == null || parm.Id <= 0)
            {
                throw new CustomException("请求参数错误");
            }
            var userId = HttpContext.GetUId();
            return SUCCESS(_DailyScheduleService.ChangeStatus(parm.Id, parm.Status, userId));
        }

        /// <summary>
        /// 删除日程
        /// </summary>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "common")]
        [Log(Title = "日程删除", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteDailySchedule(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }
            var userId = HttpContext.GetUId();
            int count = 0;
            foreach (var id in idsArr)
            {
                count += _DailyScheduleService.DeleteDailySchedule(id, userId);
            }
            return SUCCESS(count);
        }
    }
}
