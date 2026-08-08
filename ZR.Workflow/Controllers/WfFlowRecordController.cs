using Microsoft.AspNetCore.Mvc;
using ZR.Common;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 审批记录（流水轨迹）
    /// </summary>
    [Route("workflow/record")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfFlowRecordController : BaseController
    {
        private readonly IWfFlowRecordService _service;

        public WfFlowRecordController(IWfFlowRecordService service)
        {
            _service = service;
        }

        /// <summary>
        /// 审批记录列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult QueryList([FromQuery] WfFlowRecordQueryDto parm)
        {
            return SUCCESS(_service.GetList(parm));
        }

        /// <summary>
        /// 抄送给我
        /// </summary>
        [HttpGet("cc")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult CcList([FromQuery] WfFlowRecordQueryDto parm)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_service.GetCcList(parm, userId));
        }

        /// <summary>
        /// 标记抄送已读
        /// </summary>
        [HttpPost("read")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Read([FromBody] WfReadInput parm)
        {
            var userId = HttpContext.GetUId();
            _service.Read([.. Tools.SpitLongArrary(parm.Ids)], userId);
            return SUCCESS(1);
        }

        /// <summary>
        /// 抄送未读数量（用于菜单红点）
        /// </summary>
        [HttpGet("unread")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult UnreadCount()
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_service.GetUnreadCount(userId));
        }
    }
}
