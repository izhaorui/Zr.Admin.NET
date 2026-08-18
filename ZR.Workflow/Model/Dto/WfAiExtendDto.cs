using System.Collections.Generic;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// AI 审批意见建议 - 输入
    /// </summary>
    public class WfAiApprovalSuggestInput
    {
        /// <summary>
        /// 当前审批节点名称
        /// </summary>
        public string NodeName { get; set; }

        /// <summary>
        /// 表单内容（JSON 字符串，按 field 存值）
        /// </summary>
        public string FormContent { get; set; }

        /// <summary>
        /// 可选：已有草稿意见，AI 在其基础上润色
        /// </summary>
        public string DraftOpinion { get; set; }
    }

    /// <summary>
    /// AI 审批意见建议 - 输出
    /// </summary>
    public class WfAiApprovalSuggestResult
    {
        /// <summary>
        /// 建议的审批意见话术（可编辑草稿）
        /// </summary>
        public string Suggestion { get; set; }
    }

    /// <summary>
    /// AI 审批记录摘要 - 输出
    /// </summary>
    public class WfAiApprovalSummaryResult
    {
        /// <summary>
        /// 摘要文本
        /// </summary>
        public string Summary { get; set; }
    }

    /// <summary>
    /// AI 流程优化体检 - 输入
    /// </summary>
    public class WfAiFlowAnalyzeInput
    {
        /// <summary>
        /// 流程定义 Id
        /// </summary>
        public long FlowId { get; set; }
    }

    /// <summary>
    /// AI 流程优化体检 - 输出
    /// </summary>
    public class WfAiFlowAnalyzeResult
    {
        /// <summary>
        /// 整体体检结论
        /// </summary>
        public string Analysis { get; set; }

        /// <summary>
        /// 结构化优化建议列表
        /// </summary>
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// AI 自然语言发起申请 - 输入
    /// </summary>
    public class WfAiMatchFillInput
    {
        /// <summary>
        /// 用户白话描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 可选：限定在某流程定义内填单（不指定则让 AI 从全部启用流程中匹配最合适的一个）
        /// </summary>
        public long? FlowId { get; set; }
    }

    /// <summary>
    /// AI 自然语言发起申请 - 输出
    /// </summary>
    public class WfAiMatchFillResult
    {
        /// <summary>
        /// 匹配到的流程定义 Id
        /// </summary>
        public long FlowId { get; set; }

        /// <summary>
        /// 匹配到的流程名称
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// 建议预填的表单字段（field -> 字符串值），前端据此回填表单
        /// </summary>
        public Dictionary<string, string> FormContent { get; set; } = new();

        /// <summary>
        /// 匹配理由（便于用户判断是否采用）
        /// </summary>
        public string Reason { get; set; }
    }
}
