using Microsoft.AspNetCore.Mvc;
using ZR.Common;

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
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult TodoList([FromQuery] WfFlowTaskQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_taskService.GetTodoList(parm, userId));
        }

        /// <summary>
        /// 已办任务
        /// </summary>
        [HttpGet("done")]
        [ActionPermissionFilter(Permission = "common")]
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
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Read([FromBody] WfReadInput parm)
        {
            var userId = HttpContext.GetUId();
            _taskService.Read(Tools.SpitLongArrary(parm.Ids).ToList(), userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 待办未读数量（用于菜单红点）
        /// </summary>
        [HttpGet("unread")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult UnreadCount()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_taskService.GetUnreadCount(userId));
        }

        /// <summary>
        /// 批量通过（逐条复用 Approve 流转；单条失败不影响其余，返回成功/失败计数）
        /// </summary>
        [HttpPost("batchApprove")]
        [ActionPermissionFilter(Permission = "workflow:task:approve")]
        [Log(Title = "批量审批通过", BusinessType = BusinessType.UPDATE)]
        public IActionResult BatchApprove([FromBody] WfBatchApproveInput parm)
        {
            var userName = HttpContext.GetName();
            var ids = Tools.SpitLongArrary(parm.TaskIds);
            int success = 0;
            var failed = new List<string>();
            foreach (var taskId in ids)
            {
                try
                {
                    _engine.Approve(taskId, parm.Opinion, userName);
                    success++;
                }
                catch (Exception ex)
                {
                    failed.Add($"任务 {taskId}: {ex.Message}");
                }
            }
            var failedMsg = failed.Count == 0 ? null : "；" + string.Join("；", failed);
            return SUCCESS(new
            {
                success,
                failed = failed.Count,
                message = $"成功 {success} 条，失败 {failed.Count} 条{failedMsg}"
            });
        }
    }
}
