using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 工作流 AI 能力：自然语言生成流程草稿
    /// </summary>
    [Route("workflow/ai")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfAiController : BaseController
    {
        private readonly IWfAiService _service;

        public WfAiController(IWfAiService service)
        {
            _service = service;
        }

        /// <summary>
        /// AI 根据自然语言描述生成流程草稿（节点/连线/表单字段）。仅返回草稿，不直接落库。
        /// </summary>
        [HttpPost("generate")]
        [ActionPermissionFilter(Permission = "workflow:definition:ai")]
        public async Task<IActionResult> Generate([FromBody] WfAiGenerateInput input)
        {
            try
            {
                var result = await _service.GenerateFlowAsync(input);
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }
    }
}
