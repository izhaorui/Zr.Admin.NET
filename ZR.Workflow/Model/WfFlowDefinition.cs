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
        /// 流程编码（同流程多版本共享，与 Version 组合唯一）
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string FlowCode { get; set; }

        /// <summary>
        /// 版本号（同一 FlowCode 下从 1 自增；历史版本冻结保留，实例绑定各自 FlowId）
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int Version { get; set; } = 1;

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
        /// 是否草稿 0=已发布(正式版) 1=草稿(未发布)。
        /// 草稿版本不可被发起/设为现行，需 Publish 后转为已发布。
        /// 另存新版本默认生成草稿，避免误发起。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDraft { get; set; } = 0;

        /// <summary>
        /// 是否为现行版本（同 FlowCode 下唯一，Status=1 且 IsDraft=0 的版本即为现行）。
        /// 仅用于列表/版本历史展示标记，不持久化为独立列，由 Service 在查询时计算填充。
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool IsCurrent { get; set; }

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
