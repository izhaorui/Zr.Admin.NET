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
        /// 节点类型 0=开始 1=审批 2=抄送 3=结束 4=条件
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int NodeType { get; set; } = 1;

        /// <summary>
        /// 审批人类型 0=指定用户 1=指定角色 2=指定部门 3=按表单字段
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int ApproverType { get; set; } = 0;

        /// <summary>
        /// 审批人标识（ApproverType=0 时存 userId 逗号分隔；=1 角色Id；=2 部门Id；=3 表单字段 key；或签一人通过即通过）
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string ApproverId { get; set; }

        /// <summary>
        /// 审批人/抄送人 userName 快照（ApproverType=0 时与 ApproverId 同步，逗号分隔；选人时写入，显示时直接读取，不反查）
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string ApproverNames { get; set; }

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

        /// <summary>
        /// 条件字段（表单字段 key，如 amount）。为空表示无条件，节点必经。
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string ConditionField { get; set; }

        /// <summary>
        /// 条件运算符 0=无 1=小于 2=小于等于 3=大于 4=大于等于 5=等于 6=不等于
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int ConditionOp { get; set; } = 0;

        /// <summary>
        /// 条件比较值
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string ConditionValue { get; set; }

        /// <summary>
        /// 并行分组号（>0 表示参与并行分支）。同组节点同时激活待办，全部完成后汇聚；0/NULL 表示非并行。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int ParallelGroup { get; set; } = 0;
    }
}
