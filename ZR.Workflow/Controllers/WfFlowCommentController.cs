using Microsoft.AspNetCore.Mvc;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 审批评论 / 批注（独立于审批动作，不推进流程）
    /// </summary>
    [Route("workflow/comment")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfFlowCommentController : BaseController
    {
        private readonly IWfFlowCommentService _service;

        public WfFlowCommentController(IWfFlowCommentService service)
        {
            _service = service;
        }

        /// <summary>
        /// 评论列表（按流程实例，可选按节点）
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult QueryList([FromQuery] WfFlowCommentQueryDto parm)
        {
            return SUCCESS(_service.GetList(parm));
        }

        /// <summary>
        /// 新增评论
        /// </summary>
        [HttpPost("add")]
        [ActionPermissionFilter(Permission = "workflow:comment:add")]
        [Log(Title = "审批评论", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] WfFlowCommentInput parm)
        {
            var userName = HttpContext.GetName();
            var userId = HttpContext.GetUId();
            _service.Add(parm, userName, userId);
            return SUCCESS(1);
        }
    }
}
