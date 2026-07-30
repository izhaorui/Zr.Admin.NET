using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

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
            var userId = HttpContext.GetUId();
            return SUCCESS(_service.GetCcList(parm, userId));
        }

        /// <summary>
        /// 标记抄送已读
        /// </summary>
        [HttpPost("read")]
        [ActionPermissionFilter(Permission = "workflow:record:cc")]
        public IActionResult Read([FromBody] WfReadInput parm)
        {
            var userId = HttpContext.GetUId();
            _service.Read(ParseIds(parm.Ids), userId);
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
