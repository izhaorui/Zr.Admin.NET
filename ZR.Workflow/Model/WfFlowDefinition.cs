namespace ZR.Workflow.Model
{
    /// <summary>
    /// 流程定义
    /// </summary>
    [SugarTable("wf_flow_definition", "流程定义")]
    public class WfFlowDefinition : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long FlowId { get; set; }

        /// <summary>
        /// 流程编码（唯一）
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string FlowCode { get; set; }

        /// <summary>
        /// 流程名称
        /// </summary>
        [SugarColumn(Length = 100, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string FlowName { get; set; }

        /// <summary>
        /// 表单类型（预留，0=固定表单）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int FormType { get; set; } = 0;

        /// <summary>
        /// 状态 0=停用 1=启用
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int Status { get; set; } = 1;

        /// <summary>
        /// 是否删除 0=未删 1=已删（软删除，保留节点/实例/任务/记录等历史数据）
        /// </summary>
        [SugarColumn(ColumnName = "is_delete", DefaultValue = "0")]
        public int IsDelete { get; set; } = 0;

        /// <summary>
        /// 表单字段定义（JSON 数组，轻量动态表单；方案2 可替换为设计器 schema）
        /// 结构示例：[{"field":"reason","label":"请假事由","type":"textarea","required":true,"options":""}]
        /// type 取值：input|textarea|number|date|datetime|select|radio|checkbox|switch|image|user
        /// select/radio/checkbox 的 options 为逗号分隔文本（label 即 value）；user 类型存昵称字符串
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string FormItems { get; set; }
    }
}
