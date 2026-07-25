using ZR.Model.System;

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
    }
}
