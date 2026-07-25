using ZR.Model.System;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 流程节点配置
    /// </summary>
    [SugarTable("wf_flow_node", "流程节点")]
    public class WfFlowNode : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long NodeId { get; set; }

        /// <summary>
        /// 所属流程
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long FlowId { get; set; }

        /// <summary>
        /// 节点名称
        /// </summary>
        [SugarColumn(Length = 100, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string NodeName { get; set; }

        /// <summary>
        /// 节点类型 0=开始 1=审批 2=抄送 3=结束
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int NodeType { get; set; } = 1;

        /// <summary>
        /// 审批人类型 0=指定用户 1=指定角色 2=指定部门
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int ApproverType { get; set; } = 0;

        /// <summary>
        /// 审批人（ApproverType=0 时多个逗号分隔；或签一人通过即通过）
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string ApproverId { get; set; }

        /// <summary>
        /// 节点顺序（从 1 开始）
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int NodeOrder { get; set; } = 1;

        /// <summary>
        /// 签类型 0=或签 1=会签
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int SignType { get; set; } = 0;
    }
}
