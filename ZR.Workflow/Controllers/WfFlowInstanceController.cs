using Microsoft.AspNetCore.Mvc;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 流程实例（我发起的）
    /// </summary>
    [Route("workflow/instance")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfFlowInstanceController : BaseController
    {
        private readonly IWfFlowInstanceService _service;
        private readonly IWfEngineService _engine;

        public WfFlowInstanceController(IWfFlowInstanceService service, IWfEngineService engine)
        {
            _service = service;
            _engine = engine;
        }

        /// <summary>
        /// 发起申请
        /// </summary>
        [HttpPost("start")]
        [ActionPermissionFilter(Permission = "workflow:instance:start")]
        [Log(Title = "发起申请", BusinessType = BusinessType.INSERT)]
        public IActionResult Start([FromBody] WfFlowInstanceDto parm)
        {
            if (parm == null) return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            var loginUser = HttpContext.GetCurrentUser();
            var instanceId = _service.Start(parm, loginUser);
            return SUCCESS(instanceId);
        }

        /// <summary>
        /// 我发起的
        /// </summary>
        [HttpGet("my")]
        [ActionPermissionFilter(Permission = "workflow:instance:list")]
        public IActionResult MyList([FromQuery] WfFlowInstanceQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            // 管理员(IsAdmin)拥有数据权限，可查看全部用户的流程；普通用户仅看自己
            var allUser = HttpContext.IsAdmin();
            return SUCCESS(_service.GetMyList(parm, userId, allUser));
        }

        /// <summary>
        /// 实例详情
        /// </summary>
        [HttpGet("{instanceId}")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult GetInfo(long instanceId)
        {
            return SUCCESS(_service.GetInfo(instanceId));
        }

        /// <summary>
        /// 撤回申请
        /// </summary>
        [HttpPost("withdraw/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:withdraw")]
        [Log(Title = "撤回申请", BusinessType = BusinessType.UPDATE)]
        public IActionResult Withdraw(long instanceId)
        {
            var userId = HttpContext.GetUId();
            _engine.Withdraw(instanceId, userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 驳回后重新提交
        /// </summary>
        [HttpPost("resubmit/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:start")]
        [Log(Title = "重新提交", BusinessType = BusinessType.UPDATE)]
        public IActionResult Resubmit(long instanceId, [FromBody] WfFlowInstanceDto parm)
        {
            if (parm == null) return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            var userId = HttpContext.GetUId();
            // 只透传三个可编辑字段，避免前端携带整 DTO 时的歧义（Status/InstanceId 等后端字段不在变更范围内）
            _service.Resubmit(instanceId, parm.FormContent, parm.Attachment, parm.Title, userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 数据面板统计（待办/已办/我发起/抄送）
        /// </summary>
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_service.GetDashboardStats(userId));
        }

        /// <summary>
        /// 流程效率统计：平均审批时长、各节点耗时分布、完成率趋势。
        /// 管理员(IsAdmin)可查看全局数据；flowId 可选，按流程定义维度筛选。
        /// </summary>
        [HttpGet("efficiency")]
        public IActionResult Efficiency([FromQuery] long? flowId = null)
        {
            var userId = HttpContext.GetUId();
            var isAdmin = HttpContext.IsAdmin();
            return SUCCESS(_service.GetEfficiencyStats(userId, isAdmin, flowId));
        }

        #region 管理员运维操作（P0）

        /// <summary>
        /// 管理员终止 / 作废流程
        /// </summary>
        [HttpPost("terminate/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:terminate")]
        [Log(Title = "终止流程", BusinessType = BusinessType.UPDATE)]
        public IActionResult AdminTerminate(long instanceId, [FromBody] WfAdminOpinionDto parm)
        {
            var userId = HttpContext.GetUId();
            _engine.AdminTerminate(instanceId, userId, parm?.Opinion);
            return SUCCESS(1);
        }

        /// <summary>
        /// 管理员挂起流程
        /// </summary>
        [HttpPost("suspend/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:suspend")]
        [Log(Title = "挂起流程", BusinessType = BusinessType.UPDATE)]
        public IActionResult AdminSuspend(long instanceId, [FromBody] WfAdminOpinionDto parm)
        {
            var userId = HttpContext.GetUId();
            _engine.AdminSuspend(instanceId, userId, parm?.Opinion);
            return SUCCESS(1);
        }

        /// <summary>
        /// 管理员恢复被挂起的流程
        /// </summary>
        [HttpPost("resume/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:resume")]
        [Log(Title = "恢复流程", BusinessType = BusinessType.UPDATE)]
        public IActionResult AdminResume(long instanceId, [FromBody] WfAdminOpinionDto parm)
        {
            var userId = HttpContext.GetUId();
            _engine.AdminResume(instanceId, userId, parm?.Opinion);
            return SUCCESS(1);
        }

        /// <summary>
        /// 管理员改派：把指定节点的未完成任务改给目标用户
        /// </summary>
        [HttpPost("reassign/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:reassign")]
        [Log(Title = "改派流程", BusinessType = BusinessType.UPDATE)]
        public IActionResult AdminReassign(long instanceId, [FromBody] WfAdminReassignDto parm)
        {
            if (parm == null) return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            var userId = HttpContext.GetUId();
            _engine.AdminReassign(instanceId, parm.NodeId, parm.TargetUserId, userId, parm.Opinion);
            return SUCCESS(1);
        }

        /// <summary>
        /// 管理员跳转节点：把卡住的实例跳到指定节点重新激活
        /// </summary>
        [HttpPost("jump/{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:jump")]
        [Log(Title = "跳转流程", BusinessType = BusinessType.UPDATE)]
        public IActionResult AdminJump(long instanceId, [FromBody] WfAdminJumpDto parm)
        {
            if (parm == null) return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            var userId = HttpContext.GetUId();
            _engine.AdminJump(instanceId, parm.TargetNodeId, userId, parm.Opinion);
            return SUCCESS(1);
        }

        #endregion
    }
}
