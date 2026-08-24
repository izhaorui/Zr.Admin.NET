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
            var userId = HttpContext.GetUId();
            _engine.Approve(parm.TaskId, parm.Opinion, userId, parm.FormContent);
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
            var userId = HttpContext.GetUId();
            _engine.Reject(parm.TaskId, parm.Opinion, userId);
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
            var userId = HttpContext.GetUId();
            _engine.Transfer(parm.TaskId, parm.TargetUserId, parm.Opinion, userId);
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
            var userId = HttpContext.GetUId();
            _engine.AddSign(parm.TaskId, parm.UserIds, parm.Opinion, userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 委托代审（任务仍归属原审批人，仅记录代审人）
        /// </summary>
        [HttpPost("delegate")]
        [ActionPermissionFilter(Permission = "workflow:task:delegate")]
        [Log(Title = "委托代审", BusinessType = BusinessType.UPDATE)]
        public IActionResult Delegate([FromBody] WfDelegateInput parm)
        {
            var userId = HttpContext.GetUId();
            _engine.Delegate(parm.TaskId, parm.TargetUserId, parm.Opinion, userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 减签（从当前节点移除某位加签/会签待审批人，操作人须为该节点审批人之一）
        /// </summary>
        [HttpPost("removesign")]
        [ActionPermissionFilter(Permission = "workflow:task:removesign")]
        [Log(Title = "减签", BusinessType = BusinessType.UPDATE)]
        public IActionResult RemoveSign([FromBody] WfRemoveSignInput parm)
        {
            var userId = HttpContext.GetUId();
            _engine.RemoveSign(parm.TaskId, parm.TargetUserId, parm.Opinion, userId);
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
        /// 申请人催办（仅实例申请人可调用；24 小时内同实例仅可催办一次）
        /// </summary>
        [HttpPost("urge")]
        [ActionPermissionFilter(Permission = "workflow:task:urge")]
        [Log(Title = "催办", BusinessType = BusinessType.OTHER)]
        public IActionResult Urge([FromBody] WfUrgeInput parm)
        {
            var userId = HttpContext.GetUId();
            _engine.Urge(parm.InstanceId, userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 批量通过（逐条复用 Approve 流转；单条失败不影响其余，返回成功/失败计数）
        /// </summary>
        [HttpPost("batchApprove")]
        [ActionPermissionFilter(Permission = "workflow:task:approve")]
        [Log(Title = "批量审批通过", BusinessType = BusinessType.UPDATE)]
        public IActionResult BatchApprove([FromBody] WfBatchApproveInput parm)
        {
            var userId = HttpContext.GetUId();
            var ids = Tools.SpitLongArrary(parm.TaskIds);
            int success = 0;
            var failed = new List<string>();
            foreach (var taskId in ids)
            {
                try
                {
                    _engine.Approve(taskId, parm.Opinion, userId);
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
