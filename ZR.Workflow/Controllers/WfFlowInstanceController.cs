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
            return SUCCESS(_service.GetMyList(parm, userId));
        }

        /// <summary>
        /// 实例详情
        /// </summary>
        [HttpGet("{instanceId}")]
        [ActionPermissionFilter(Permission = "workflow:instance:list")]
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
            var userName = HttpContext.GetName();
            _engine.Withdraw(instanceId, userName);
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
            var userName = HttpContext.GetName();
            _service.Resubmit(instanceId, parm, userName);
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
    }
}
