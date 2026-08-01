using Microsoft.AspNetCore.Mvc;
using ZR.Common;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 表单模板管理：可复用的动态表单，供流程定义设计器载入复用
    /// </summary>
    [Route("workflow/formTemplate")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfFormTemplateController : BaseController
    {
        private readonly IWfFormTemplateService _service;

        public WfFormTemplateController(IWfFormTemplateService service)
        {
            _service = service;
        }

        /// <summary>
        /// 表单模板列表
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult QueryList([FromQuery] WfFormTemplateQueryDto parm)
        {
            return SUCCESS(_service.GetList(parm));
        }

        /// <summary>
        /// 表单模板详情
        /// </summary>
        [HttpGet("{formId}")]
        public IActionResult GetInfo(long formId)
        {
            return SUCCESS(_service.GetInfo(formId));
        }

        /// <summary>
        /// 新增表单模板
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "workflow:template:add")]
        [Log(Title = "表单模板", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] WfFormTemplateDto parm)
        {
            if (parm == null) return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            var response = _service.Add(parm.ToCreate());
            return SUCCESS(response);
        }

        /// <summary>
        /// 修改表单模板
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "workflow:template:edit")]
        [Log(Title = "表单模板", BusinessType = BusinessType.UPDATE)]
        public IActionResult Edit([FromBody] WfFormTemplateDto parm)
        {
            var response = _service.Update(parm.ToUpdate());
            return SUCCESS(response);
        }

        /// <summary>
        /// 删除表单模板
        /// </summary>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "workflow:template:delete")]
        [Log(Title = "表单模板", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(string ids)
        {
            var idArr = Tools.SpitLongArrary(ids);
            var result = _service.Delete(idArr);
            return SUCCESS(result);
        }
    }
}
