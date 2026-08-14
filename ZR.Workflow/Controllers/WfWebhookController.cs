using Microsoft.AspNetCore.Mvc;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 工作流 Webhook 配置管理
    /// </summary>
    [Route("workflow/webhook")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfWebhookController : BaseController
    {
        private readonly IWfWebhookService _service;

        public WfWebhookController(IWfWebhookService service)
        {
            _service = service;
        }

        /// <summary>
        /// Webhook 配置列表(设计器下拉选择也会使用)
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult QueryList([FromQuery] WfWebhook parm)
        {
            return SUCCESS(_service.GetList(parm));
        }

        /// <summary>
        /// 新增配置
        /// </summary>
        [HttpPost]
        [Log(Title = "Webhook配置", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "workflow:webhook:add")]
        public IActionResult Add([FromBody] WfWebhook parm)
        {
            return SUCCESS(_service.Add(parm));
        }

        /// <summary>
        /// 修改配置
        /// </summary>
        [HttpPut]
        [Log(Title = "Webhook配置", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "workflow:webhook:edit")]
        public IActionResult Update([FromBody] WfWebhook parm)
        {
            return SUCCESS(_service.Update(parm));
        }

        /// <summary>
        /// 删除配置
        /// </summary>
        [HttpPost("delete/{ids}")]
        [Log(Title = "Webhook配置", BusinessType = BusinessType.DELETE)]
        [ActionPermissionFilter(Permission = "workflow:webhook:delete")]
        public IActionResult Delete(long[] ids)
        {
            return SUCCESS(_service.Delete(ids));
        }
    }
}
