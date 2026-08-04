using Microsoft.AspNetCore.Mvc;
using ZR.Common;

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
            return SUCCESS(_service.Add(parm.ToCreate()));
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

        /// <summary>
        /// 复制流程定义（含节点配置），生成停用状态的副本
        /// </summary>
        [HttpPost("copy/{flowId}")]
        [ActionPermissionFilter(Permission = "workflow:definition:add")]
        [Log(Title = "流程定义", BusinessType = BusinessType.INSERT)]
        public IActionResult Copy(long flowId)
        {
            var userName = HttpContext.GetName();
            var newId = _service.Copy(flowId, userName);
            return SUCCESS(newId);
        }

        /// <summary>
        /// 另存为新版本：复制当前定义与节点到新 FlowId，Version 自增，旧版本冻结保留
        /// </summary>
        [HttpPost("saveAsNewVersion/{flowId}")]
        [ActionPermissionFilter(Permission = "workflow:definition:add")]
        [Log(Title = "流程定义", BusinessType = BusinessType.INSERT)]
        public IActionResult SaveAsNewVersion(long flowId)
        {
            var userName = HttpContext.GetName();
            var newId = _service.SaveAsNewVersion(flowId, userName);
            return SUCCESS(newId);
        }

        /// <summary>
        /// 查询某流程编码下的全部版本（版本历史）
        /// </summary>
        [HttpGet("versions")]
        [ActionPermissionFilter(Permission = "workflow:definition:list")]
        public IActionResult GetVersions([FromQuery] string flowCode)
        {
            return SUCCESS(_service.GetVersions(flowCode));
        }

        /// <summary>
        /// 设为现行版本：启用目标版本并停用同 FlowCode 下其他版本，保证现行版本唯一
        /// </summary>
        [HttpPost("setCurrent/{flowId}")]
        [ActionPermissionFilter(Permission = "workflow:definition:edit")]
        [Log(Title = "流程定义", BusinessType = BusinessType.UPDATE)]
        public IActionResult SetCurrentVersion(long flowId)
        {
            var userName = HttpContext.GetName();
            return SUCCESS(_service.SetCurrentVersion(flowId, userName));
        }

        /// <summary>
        /// 发布草稿版本：将草稿(IsDraft=1)转为正式(IsDraft=0)
        /// </summary>
        [HttpPost("publish/{flowId}")]
        [ActionPermissionFilter(Permission = "workflow:definition:edit")]
        [Log(Title = "流程定义", BusinessType = BusinessType.UPDATE)]
        public IActionResult Publish(long flowId)
        {
            var userName = HttpContext.GetName();
            return SUCCESS(_service.Publish(flowId, userName));
        }

        /// <summary>
        /// 版本回滚：将指定历史版本复制为新的最高版本（草稿态），保留完整版本链路
        /// </summary>
        [HttpPost("rollback/{flowId}")]
        [ActionPermissionFilter(Permission = "workflow:definition:edit")]
        [Log(Title = "流程定义", BusinessType = BusinessType.INSERT)]
        public IActionResult Rollback(long flowId)
        {
            var userName = HttpContext.GetName();
            var newId = _service.Rollback(flowId, userName);
            return SUCCESS(newId);
        }
    }
}
