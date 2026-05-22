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
    }
}
