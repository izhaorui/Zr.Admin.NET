namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// AI 自然语言生成流程草稿 - 请求
    /// </summary>
    public class WfAiGenerateInput
    {
        /// <summary>自然语言流程描述，如「申请人→部门负责人→金额≥10万:法务+财务→总监→归档抄送申请人」</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// AI 生成的流程草稿（节点数组 + 连线数组 + 表单字段），仅作设计器渲染草稿，不直接入库
    /// </summary>
    public class WfAiGenerateResultDto
    {
        /// <summary>节点数组，按 index 顺序，连线用 sourceIndex/targetIndex 引用</summary>
        public List<WfAiNodeDto> Nodes { get; set; } = new();

        /// <summary>连线数组</summary>
        public List<WfAiLinkDto> Links { get; set; } = new();

        /// <summary>表单字段（动态表单 formItems），AI 从描述中抽取</summary>
        public List<WfAiFormFieldDto> FormItems { get; set; } = new();
    }

    /// <summary>
    /// AI 生成的节点。nodeType 对齐前端 flowDict NODE_TYPE：
    /// 1 审批 / 2 抄送 / 4 条件网关 / 7 并行分叉 / 8 并行汇聚
    /// </summary>
    public class WfAiNodeDto
    {
        /// <summary>节点类型：1审批 2抄送 4条件网关 7并行分叉 8并行汇聚</summary>
        public int NodeType { get; set; }

        /// <summary>节点名称，如「部门负责人」</summary>
        public string NodeName { get; set; }

        /// <summary>审批人类型：0指定用户 4部门负责人 5发起人主管（抄送/条件/并行节点可不填）</summary>
        public int? ApproverType { get; set; }

        /// <summary>审批人/抄送人 userId 列表（逗号分隔），ApproverType=0 时使用</summary>
        public string ApproverIds { get; set; }

        /// <summary>审批人/抄送人姓名快照（逗号分隔），用于前端展示</summary>
        public string ApproverNames { get; set; }

        /// <summary>会签/或签：0或签（任一通过） 1会签（全部通过）。仅审批节点有意义</summary>
        public int? SignType { get; set; }

        /// <summary>并行分组号，并行分叉(7)/汇聚(8) 同一组的编号须一致</summary>
        public int ParallelGroup { get; set; }

        /// <summary>条件节点的分支（仅 NodeType=4 时由后端按 Links 推导，可不填）</summary>
        public List<WfAiBranchDto> Branches { get; set; }
    }

    /// <summary>
    /// AI 生成的连线。sourceIndex/targetIndex 为 Nodes 数组下标（从 0 开始）
    /// </summary>
    public class WfAiLinkDto
    {
        /// <summary>源节点在 Nodes 数组中的下标</summary>
        public int SourceIndex { get; set; }

        /// <summary>目标节点在 Nodes 数组中的下标</summary>
        public int TargetIndex { get; set; }

        /// <summary>条件字段 key（如 amount）。仅条件网关出边使用，须存在于 FormItems 的 field；空串表示默认分支</summary>
        public string Field { get; set; }

        /// <summary>条件运算符：0无 1小于 2小于等于 3大于 4大于等于 5等于 6不等于</summary>
        public int Op { get; set; }

        /// <summary>条件比较值（字符串，如 "100000"）</summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// AI 生成的条件分支（保留结构，便于与前端 branches 对齐）
    /// </summary>
    public class WfAiBranchDto
    {
        public string Target { get; set; }
        public string Field { get; set; }
        public int Op { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// AI 抽取的表单字段，对齐前端 formItems 结构：{ field, label, type, required, options? }
    /// </summary>
    public class WfAiFormFieldDto
    {
        /// <summary>字段 key，如 amount / contractName</summary>
        public string Field { get; set; }

        /// <summary>展示名，如「合同金额」「合同名称」</summary>
        public string Label { get; set; }

        /// <summary>类型：input/textarea/number/date/datetime/select/radio/switch/image</summary>
        public string Type { get; set; } = "input";

        /// <summary>是否必填</summary>
        public bool Required { get; set; }

        /// <summary>选项（select/radio 用，逗号分隔；无则空）</summary>
        public string Options { get; set; }
    }
}
