using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 审批任务（待我审批 / 已办）
    /// </summary>
    [Route("workflow/task")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfFlowTaskController : BaseController
    {
        private readonly IWfFlowTaskService _taskService;
        private readonly IWfEngineService _engine;

        public WfFlowTaskController(IWfFlowTaskService taskService, IWfEngineService engine)
        {
            _taskService = taskService;
            _engine = engine;
        }

        /// <summary>
        /// 待我审批
        /// </summary>
        [HttpGet("todo")]
        [ActionPermissionFilter(Permission = "workflow:task:list")]
        public IActionResult TodoList([FromQuery] WfFlowTaskQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_taskService.GetTodoList(parm, userId));
        }

        /// <summary>
        /// 已办任务
        /// </summary>
        [HttpGet("done")]
        [ActionPermissionFilter(Permission = "workflow:task:list")]
        public IActionResult DoneList([FromQuery] WfFlowTaskQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_taskService.GetDoneList(parm, userId));
        }

        /// <summary>
        /// 通过
        /// </summary>
        [HttpPost("approve")]
        [ActionPermissionFilter(Permission = "workflow:task:approve")]
        [Log(Title = "审批通过", BusinessType = BusinessType.UPDATE)]
        public IActionResult Approve([FromBody] WfApproveInput parm)
        {
            var userName = HttpContext.GetName();
            _engine.Approve(parm.TaskId, parm.Opinion, userName);
            return SUCCESS(1);
        }

        /// <summary>
        /// 驳回
        /// </summary>
        [HttpPost("reject")]
        [ActionPermissionFilter(Permission = "workflow:task:reject")]
        [Log(Title = "审批驳回", BusinessType = BusinessType.UPDATE)]
        public IActionResult Reject([FromBody] WfApproveInput parm)
        {
            var userName = HttpContext.GetName();
            _engine.Reject(parm.TaskId, parm.Opinion, userName);
            return SUCCESS(1);
        }

        /// <summary>
        /// 转办
        /// </summary>
        [HttpPost("transfer")]
        [ActionPermissionFilter(Permission = "workflow:task:transfer")]
        [Log(Title = "转办", BusinessType = BusinessType.UPDATE)]
        public IActionResult Transfer([FromBody] WfTransferInput parm)
        {
            var userName = HttpContext.GetName();
            _engine.Transfer(parm.TaskId, parm.TargetUser, parm.Opinion, userName);
            return SUCCESS(1);
        }

        /// <summary>
        /// 加签
        /// </summary>
        [HttpPost("addsign")]
        [ActionPermissionFilter(Permission = "workflow:task:addsign")]
        [Log(Title = "加签", BusinessType = BusinessType.UPDATE)]
        public IActionResult AddSign([FromBody] WfAddSignInput parm)
        {
            var userName = HttpContext.GetName();
            _engine.AddSign(parm.TaskId, parm.Users, parm.Opinion, userName);
            return SUCCESS(1);
        }

        /// <summary>
        /// 标记待办已读
        /// </summary>
        [HttpPost("read")]
        [ActionPermissionFilter(Permission = "workflow:task:list")]
        public IActionResult Read([FromBody] WfReadInput parm)
        {
            var userId = HttpContext.GetUId();
            _taskService.Read(ParseIds(parm.Ids), userId);
            return SUCCESS(1);
        }

        private static List<long> ParseIds(string ids)
        {
            var result = new List<long>();
            if (string.IsNullOrEmpty(ids)) return result;
            foreach (var s in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (long.TryParse(s, out var v)) result.Add(v);
            }
            return result;
        }
    }
}
