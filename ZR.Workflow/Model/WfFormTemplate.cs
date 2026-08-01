namespace ZR.Workflow.Model
{
    /// <summary>
    /// 表单模板：可复用的动态表单定义，供流程定义在设计器中"载入模板"复用，避免重复搭建表单。
    /// 与流程定义为拷贝语义（载入即将模板 FormItems 复制到定义），不强制共享，已发起实例的表单仍按各自定义冻结。
    /// </summary>
    [SugarTable("wf_form_template", "表单模板")]
    public class WfFormTemplate : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long FormId { get; set; }

        /// <summary>
        /// 模板名称
        /// </summary>
        [SugarColumn(Length = 100, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string FormName { get; set; }

        /// <summary>
        /// 状态 0=停用 1=启用
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int Status { get; set; } = 1;

        /// <summary>
        /// 是否删除 0=未删 1=已删（软删除）
        /// </summary>
        [SugarColumn(ColumnName = "is_delete", DefaultValue = "0")]
        public int IsDelete { get; set; } = 0;

        /// <summary>
        /// 表单字段定义（JSON 数组，与 WfFlowDefinition.FormItems 同结构）。
        /// 结构示例：[{"field":"reason","label":"请假事由","type":"textarea","required":true,"options":""}]
        /// type 取值：input|textarea|number|date|datetime|select|radio|checkbox|switch|image|user
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string FormItems { get; set; }
    }
}
