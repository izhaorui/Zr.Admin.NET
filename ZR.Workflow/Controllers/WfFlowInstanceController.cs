using Infrastructure.Extensions;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

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
            var userName = HttpContext.GetName();
            var instanceId = _service.Start(parm, userName);
            return SUCCESS(instanceId);
        }

        /// <summary>
        /// 我发起的
        /// </summary>
        [HttpGet("my")]
        [ActionPermissionFilter(Permission = "workflow:instance:list")]
        public IActionResult MyList([FromQuery] WfFlowInstanceQueryDto parm)
        {
            var userName = HttpContext.GetName();
            return SUCCESS(_service.GetMyList(parm, userName));
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
    }
}
