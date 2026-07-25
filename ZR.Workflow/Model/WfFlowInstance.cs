using ZR.Model.System;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 流程实例（一次具体申请）
    /// </summary>
    [SugarTable("wf_flow_instance", "流程实例")]
    public class WfFlowInstance : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long InstanceId { get; set; }

        /// <summary>
        /// 流程定义Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long FlowId { get; set; }

        /// <summary>
        /// 流程名称（冗余）
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string FlowName { get; set; }

        /// <summary>
        /// 业务标识（外部单据关联键，预留）
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string BusinessKey { get; set; }

        /// <summary>
        /// 申请标题
        /// </summary>
        [SugarColumn(Length = 200, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Title { get; set; }

        /// <summary>
        /// 申请人
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string ApplyUser { get; set; }

        /// <summary>
        /// 实例状态 0=审批中 1=通过 2=驳回 3=撤回
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; } = 0;

        /// <summary>
        /// 当前节点Id
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? CurrentNodeId { get; set; }

        /// <summary>
        /// 表单内容（JSON，预留动态表单）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string FormContent { get; set; }

        /// <summary>
        /// 附件路径（多个逗号分隔）
        /// </summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string Attachment { get; set; }
    }
}
