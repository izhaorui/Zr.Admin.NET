using Infrastructure.Extensions;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 流程定义管理
    /// </summary>
    [Route("workflow/definition")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfFlowDefinitionController : BaseController
    {
        private readonly IWfFlowDefinitionService _service;

        public WfFlowDefinitionController(IWfFlowDefinitionService service)
        {
            _service = service;
        }

        /// <summary>
        /// 流程定义列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "workflow:definition:list")]
        public IActionResult QueryList([FromQuery] WfFlowDefinitionQueryDto parm)
        {
            return SUCCESS(_service.GetList(parm));
        }

        /// <summary>
        /// 流程定义详情（含节点）
        /// </summary>
        [HttpGet("{flowId}")]
        public IActionResult GetInfo(long flowId)
        {
            return SUCCESS(_service.GetInfo(flowId));
        }

        /// <summary>
        /// 节点列表
        /// </summary>
        [HttpGet("nodes/{flowId}")]
        [ActionPermissionFilter(Permission = "workflow:definition:list")]
        public IActionResult GetNodes(long flowId)
        {
            return SUCCESS(_service.GetNodes(flowId));
        }

        /// <summary>
        /// 新增流程定义
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "workflow:definition:add")]
        [Log(Title = "流程定义", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] WfFlowDefinitionDto parm)
        {
            if (parm == null) return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            var response = _service.Add(parm.ToCreate());
            return SUCCESS(response);
        }

        /// <summary>
        /// 修改流程定义
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "workflow:definition:edit")]
        [Log(Title = "流程定义", BusinessType = BusinessType.UPDATE)]
        public IActionResult Edit([FromBody] WfFlowDefinitionDto parm)
        {
            var response = _service.Update(parm.ToUpdate());
            return SUCCESS(response);
        }

        /// <summary>
        /// 删除流程定义
        /// </summary>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "workflow:definition:delete")]
        [Log(Title = "流程定义", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(string ids)
        {
            var idArr = Tools.SpitLongArrary(ids);
            var result = _service.Delete(idArr);
            return SUCCESS(result);
        }
    }
}
