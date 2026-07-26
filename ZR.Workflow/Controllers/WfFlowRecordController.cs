using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

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
        [ActionPermissionFilter(Permission = "workflow:record:list")]
        public IActionResult QueryList([FromQuery] WfFlowRecordQueryDto parm)
        {
            return SUCCESS(_service.GetList(parm));
        }

        /// <summary>
        /// 抄送给我
        /// </summary>
        [HttpGet("cc")]
        [ActionPermissionFilter(Permission = "workflow:record:cc")]
        public IActionResult CcList([FromQuery] WfFlowRecordQueryDto parm)
        {
            var userName = HttpContext.GetName();
            return SUCCESS(_service.GetCcList(parm, userName));
        }
    }
}
