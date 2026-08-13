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
        /// 审批人类型 0=指定用户 1=指定角色 2=指定部门 3=按表单字段 4=部门负责人 5=发起人主管
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

        /// <summary>
        /// 节点进入事件钩子（Webhook URL）。节点被引擎到达（生成待办/抄送前）时异步 POST 通知，失败仅记日志不阻断流程。
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string EnterHookUrl { get; set; }

        /// <summary>
        /// 节点离开事件钩子（Webhook URL）。节点审批/抄送完成后、推进到下一节点前异步 POST 通知，失败仅记日志不阻断流程。
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string LeaveHookUrl { get; set; }

        /// <summary>
        /// 驳回策略（对应 WfRejectStrategy）：0=驳回发起人（默认）；1=驳回到上一审批节点；2=驳回到指定节点。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int RejectStrategy { get; set; } = 0;

        /// <summary>
        /// 驳回目标节点（RejectStrategy=2 时生效）：驳回后回到该节点重新审批。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? RejectTargetNodeId { get; set; }

        /// <summary>
        /// 空审批人兜底策略（对应 WfEmptyApproverStrategy）：0=自动通过（默认）；1=指定默认审批人。
        /// 当节点审批人为空时按此策略处理，避免流程因无人审批而卡死。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int EmptyApproverStrategy { get; set; } = 0;

        /// <summary>
        /// 兜底默认审批人（EmptyApproverStrategy=1 时生效）：审批人为空时退回到该 userId 代为审批；为空则退化为自动通过。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? DefaultApproverId { get; set; }

        /// <summary>
        /// 节点超时时长（小时）。0=不超时（默认）；>0 时引擎在生成待办时计算 DeadlineTime=ArriveTime+TimeoutHours。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int TimeoutHours { get; set; } = 0;

        /// <summary>
        /// 超时动作（对应 WfTimeoutAction）：待办超过 DeadlineTime 后由定时任务自动处理。
        /// 0=不处理（默认）；1=自动通过；2=自动驳回；3=转交指定人（TimeoutTransferUserId）。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int TimeoutAction { get; set; } = 0;

        /// <summary>
        /// 超时转交目标用户（TimeoutAction=3 时生效）：超时后把待办转给该 userId 接手；为空则退化为自动通过。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? TimeoutTransferUserId { get; set; }
    }
}
