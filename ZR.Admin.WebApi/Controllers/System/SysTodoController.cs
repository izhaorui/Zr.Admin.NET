using Microsoft.AspNetCore.Mvc;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 个人待办Controller
    /// </summary>
    [Route("system/todo")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysTodoController : BaseController
    {
        private readonly ISysTodoService _SysTodoService;

        public SysTodoController(ISysTodoService SysTodoService)
        {
            _SysTodoService = SysTodoService;
        }

        /// <summary>
        /// 查询个人待办列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:todo:list")]
        public IActionResult QuerySysTodo([FromQuery] SysTodoQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            var response = _SysTodoService.GetPages(parm, userId);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询统计（总/未完成/今日到期/已逾期）
        /// </summary>
        [HttpGet("stats")]
        [ActionPermissionFilter(Permission = "system:todo:list")]
        public IActionResult GetStats()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_SysTodoService.GetStats(userId));
        }

        /// <summary>
        /// 查询当前用户未完成待办列表（供消息中心待办 tab 打开时拉取，不写消息）
        /// </summary>
        [HttpGet("reminders")]
        [ActionPermissionFilter(Permission = "system:todo:list")]
        public IActionResult GetReminders()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_SysTodoService.GetReminderTodos(userId));
        }

        /// <summary>
        /// 查询详情
        /// </summary>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "system:todo:query")]
        public IActionResult GetSysTodo(long id)
        {
            var userId = HttpContext.GetUId();
            var response = _SysTodoService.GetById(id, userId);
            return SUCCESS(response);
        }

        /// <summary>
        /// 新增个人待办
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:todo:add")]
        [Log(Title = "个人待办新增", BusinessType = BusinessType.INSERT)]
        public IActionResult AddSysTodo([FromBody] SysTodoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            var userId = HttpContext.GetUId();
            var model = parm.Adapt<SysTodo>().ToCreate(HttpContext);
            model.UserId = userId;
            return SUCCESS(_SysTodoService.AddSysTodo(model));
        }

        /// <summary>
        /// 修改个人待办
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:todo:edit")]
        [Log(Title = "个人待办修改", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateSysTodo([FromBody] SysTodoDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求实体不能为空");
            }
            var userId = HttpContext.GetUId();
            var model = parm.Adapt<SysTodo>().ToUpdate(HttpContext);
            model.UserId = userId;
            return SUCCESS(_SysTodoService.UpdateSysTodo(model));
        }

        /// <summary>
        /// 标记完成 / 取消完成
        /// </summary>
        [HttpPut("changeStatus")]
        [ActionPermissionFilter(Permission = "system:todo:edit")]
        [Log(Title = "个人待办状态切换", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeStatus([FromBody] SysTodoStatusDto parm)
        {
            if (parm == null || parm.Id <= 0)
            {
                throw new CustomException("请求参数错误");
            }
            var userId = HttpContext.GetUId();
            return SUCCESS(_SysTodoService.ChangeStatus(parm.Id, parm.Status, userId));
        }

        /// <summary>
        /// 删除个人待办
        /// </summary>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "system:todo:remove")]
        [Log(Title = "个人待办删除", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteSysTodo(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }
            var userId = HttpContext.GetUId();
            int count = 0;
            foreach (var id in idsArr)
            {
                count += _SysTodoService.DeleteSysTodo(id, userId);
            }
            return SUCCESS(count);
        }
    }
}
