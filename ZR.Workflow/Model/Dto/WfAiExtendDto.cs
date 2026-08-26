using System.Text.Json;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 风险等级枚举：低 / 中 / 高。JSON 序列化保持 low/mid/high（兼容 LLM 契约与前端）。
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(WfRiskLevelConverter))]
    public enum WfRiskLevel
    {
        Low = 0,
        Mid = 1,
        High = 2
    }

    /// <summary>
    /// WfRiskLevel 的 JSON 转换器：序列化输出 low/mid/high，反序列化大小写不敏感接受 low/mid/high，非法/空→Low。
    /// 不用内置 JsonStringEnumConverter：其遇非法值抛异常，此处需容错降级为 Low（LLM 输出不可控）。
    /// </summary>
    public sealed class WfRiskLevelConverter : System.Text.Json.Serialization.JsonConverter<WfRiskLevel>
    {
        public override WfRiskLevel Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString()?.Trim().ToLowerInvariant() switch
                {
                    "mid" => WfRiskLevel.Mid,
                    "high" => WfRiskLevel.High,
                    _ => WfRiskLevel.Low
                };
            }
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
            {
                return n switch { 1 => WfRiskLevel.Mid, 2 => WfRiskLevel.High, _ => WfRiskLevel.Low };
            }
            return WfRiskLevel.Low;
        }

        public override void Write(Utf8JsonWriter writer, WfRiskLevel value, JsonSerializerOptions options)
            => writer.WriteStringValue(value switch
            {
                WfRiskLevel.Mid => "mid",
                WfRiskLevel.High => "high",
                _ => "low"
            });
    }

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
        /// 流程实例 Id（用于把表单字段技术名翻译为中文属性名，避免 input_1 等暴露给 AI/用户）
        /// </summary>
        public long InstanceId { get; set; }

        /// <summary>
        /// 可选：已有草稿意见，AI 在其基础上润色
        /// </summary>
        public string DraftOpinion { get; set; }

        /// <summary>
        /// 可选：表单中的图片附件 URL 列表（完整 http 地址），交由视觉模型多模态理解。
        /// 为空时走纯文本管线；非空时自动切换到 VisionModel（未配置则抛友好提示）。
        /// 建议优先由 AttachmentParsed 派生，前端无需单独收集。
        /// </summary>
        public List<string> ImageUrls { get; set; }

        /// <summary>
        /// 可选：提交时异步填充的附件解析结果（WfFlowInstance.AttachmentParsed，JSON 数组）。
        /// 非空时优先用于渲染表单上下文（文件文本直接复用，不再重复下载/抽取），
        /// 并从中派生图片 URL 列表；为空时降级用 FormContent/ImageUrls。
        /// </summary>
        public string AttachmentParsed { get; set; }
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

    /// <summary>
    /// AI 审批链汇总 - 输出：对某实例的完整审批过程做结构化结论 / 风险提示 / 改进建议。
    /// </summary>
    public class WfAiInstanceSummaryResult
    {
        /// <summary>
        /// 审批全过程结论（一段话，说明流程走向与最终结果）
        /// </summary>
        public string Conclusion { get; set; }

        /// <summary>
        /// 风险等级：low / mid / high（低 / 中 / 高）
        /// </summary>
        public WfRiskLevel RiskLevel { get; set; } = WfRiskLevel.Low;

        /// <summary>
        /// 风险提示列表（如金额异常、意见分歧、驳回反复等）
        /// </summary>
        public List<string> Risks { get; set; } = new();

        /// <summary>
        /// 改进建议列表（对流程或申请人/审批人的优化建议）
        /// </summary>
        public List<string> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// AI 审批风险预判 - 输出：站在当前节点审批人视角，对某待审批申请做风险提示。
    /// </summary>
    public class WfAiRiskCheckResult
    {
        /// <summary>
        /// 风险等级：low / mid / high（低 / 中 / 高）
        /// </summary>
        public WfRiskLevel RiskLevel { get; set; } = WfRiskLevel.Low;

        /// <summary>
        /// 风险提示列表（如金额异常、缺附件、历史驳回反复等）
        /// </summary>
        public List<string> Risks { get; set; } = new();

        /// <summary>
        /// 给当前审批人的可执行建议列表
        /// </summary>
        public List<string> Suggestions { get; set; } = new();
    }
}
