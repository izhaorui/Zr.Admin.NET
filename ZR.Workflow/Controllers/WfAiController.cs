using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Workflow.Helper;

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
                // 把表单字段技术名翻译为中文属性名，避免 input_1 等暴露给 AI/用户
                if (input.InstanceId > 0)
                {
                    if (!string.IsNullOrWhiteSpace(input.FormContent))
                    {
                        input.FormContent = await _instanceService.TranslateFormContent(input.InstanceId, input.FormContent);
                    }
                    // 附件解析结果以服务端落库值为准，不信任客户端传值（防伪造污染 AI 上下文）
                    input.AttachmentParsed = await _instanceService.GetInstanceAttachmentParsed(input.InstanceId);
                }
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
                var result = await _instanceService.SummarizeInstance(instanceId, HttpContext.GetUId(), HttpContext.IsAdmin());
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// AI 审批风险预判：站在当前节点审批人视角，对待办任务对应申请做风险提示
        /// </summary>
        [HttpPost("risk-check/{taskId}")]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> RiskCheck(long taskId)
        {
            try
            {
                var result = await _instanceService.TaskRiskCheck(taskId, HttpContext.GetUId());
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        //[HttpGet("test")]
        //[AllowAnonymous]
        //public async Task<IActionResult> Test(long recordId)
        //{
        //    var url = "http://192.168.31.184:8888/2026/0826/1bb77a40e2c4ef08.docx";
        //    var text = await WfAttachmentHelper.ExtractTextAsync(url).ConfigureAwait(false);
        //    return SUCCESS(text);
        //}
    }
}
