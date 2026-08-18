using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 工作流 AI 服务：将自然语言流程描述解析为可编辑草稿（节点/连线/表单字段）
    /// </summary>
    public interface IWfAiService
    {
        /// <summary>
        /// 根据自然语言描述生成流程草稿。未配置 AI 或解析失败时抛出异常，由 Controller 捕获返回友好提示。
        /// </summary>
        Task<WfAiGenerateResultDto> GenerateFlowAsync(WfAiGenerateInput input);

        /// <summary>
        /// 提交前审批意见话术建议
        /// </summary>
        Task<WfAiApprovalSuggestResult> SuggestApprovalAsync(WfAiApprovalSuggestInput input);

        /// <summary>
        /// 提交后审批记录摘要（落痕用）
        /// </summary>
        Task<WfAiApprovalSummaryResult> SummarizeApprovalAsync(string action, string nodeName, string opinion, string formContent);

        /// <summary>
        /// 流程优化体检
        /// </summary>
        Task<WfAiFlowAnalyzeResult> AnalyzeFlowAsync(WfAiFlowAnalyzeInput input);

        /// <summary>
        /// 自然语言发起申请（Web 端匹配流程 + 预填表单）
        /// </summary>
        Task<WfAiMatchFillResult> MatchAndFillFormAsync(WfAiMatchFillInput input);
    }
}
