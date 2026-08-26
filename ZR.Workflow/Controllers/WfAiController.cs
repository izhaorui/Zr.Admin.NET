using Microsoft.AspNetCore.Mvc;

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
        private readonly IWfFlowInstanceService _instanceService;

        public WfAiController(IWfAiService service, IWfFlowInstanceService instanceService)
        {
            _service = service;
            _instanceService = instanceService;
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

        /// <summary>
        /// 提交前 AI 审批意见话术建议（可编辑草稿，不落库）
        /// </summary>
        [HttpPost("approval-suggest")]
        [ActionPermissionFilter(Permission = "workflow:task:ai")]
        public async Task<IActionResult> ApprovalSuggest([FromBody] WfAiApprovalSuggestInput input)
        {
            try
            {
                var result = await _service.SuggestApprovalAsync(input);
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// AI 流程优化体检（对已有流程定义做结构化优化建议）
        /// </summary>
        [HttpPost("flow-analyze")]
        [ActionPermissionFilter(Permission = "workflow:definition:ai-analyze")]
        public async Task<IActionResult> FlowAnalyze([FromBody] WfAiFlowAnalyzeInput input)
        {
            try
            {
                var result = await _service.AnalyzeFlowAsync(input);
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// AI 自然语言发起申请（Web 端：匹配最合适流程 + 预填表单字段）
        /// </summary>
        [HttpPost("match-fill")]
        [ActionPermissionFilter(Permission = "workflow:instance:ai-fill")]
        public async Task<IActionResult> MatchFill([FromBody] WfAiMatchFillInput input)
        {
            try
            {
                var result = await _service.MatchAndFillFormAsync(input);
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// AI 审批链汇总：对某实例的完整审批过程生成结论 / 风险提示 / 改进建议
        /// </summary>
        [HttpPost("instance-summary/{instanceId}")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> InstanceSummary(long instanceId)
        {
            try
            {
                var result = await _instanceService.SummarizeInstance(instanceId);
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }
    }
}
