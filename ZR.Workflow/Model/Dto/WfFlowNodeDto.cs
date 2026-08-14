namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程节点输入输出对象
    /// </summary>
    public class WfFlowNodeDto : SysBase
    {
        /// <summary>节点Id（新增时不传）</summary>
        public long NodeId { get; set; }

        /// <summary>流程定义Id</summary>
        public long FlowId { get; set; }

        /// <summary>节点名称</summary>
        [Required(ErrorMessage = "节点名称不能为空")]
        public string NodeName { get; set; }

        /// <summary>节点类型 0=开始 1=审批 2=抄送 3=结束 4=条件网关 7=并行分叉网关 8=并行汇聚网关（7/8 不配审批人，本身不生成任务）</summary>
        public int NodeType { get; set; } = 1;

        /// <summary>审批人类型 0=指定用户 1=指定角色 2=部门主管 3=表单字段 4=部门负责人 5=发起人主管（开始/结束等非审批节点不传）</summary>
        public int? ApproverType { get; set; }

        /// <summary>审批人（多个逗号分隔）</summary>
        public string ApproverId { get; set; }

        /// <summary>审批人/抄送人 userName 快照（多个逗号分隔，与 ApproverId 同步）</summary>
        public string ApproverNames { get; set; }

        /// <summary>节点顺序（仅展示/兜底排序用；串联走向由 NodeLinks 决定）</summary>
        public int NodeOrder { get; set; } = 1;

        /// <summary>签类型 0=或签 1=会签（开始/结束等非审批节点不传）</summary>
        public int? SignType { get; set; }

        /// <summary>条件字段（表单字段 key，如 amount），为空表示无条件</summary>
        public string ConditionField { get; set; }

        /// <summary>条件运算符 0=无 1=小于 2=小于等于 3=大于 4=大于等于 5=等于 6=不等于</summary>
        public int? ConditionOp { get; set; }

        /// <summary>条件比较值</summary>
        public string ConditionValue { get; set; }

        /// <summary>并行分组号（>0 表示并行分支，同组并发并汇聚；非并行节点不传）</summary>
        public int? ParallelGroup { get; set; }

        /// <summary>节点进入事件钩子关联的 Webhook 配置Id（WfWebhook.WebhookId），为空不通知。</summary>
        public long? EnterWebhookId { get; set; }

        /// <summary>节点离开事件钩子关联的 Webhook 配置Id（WfWebhook.WebhookId），为空不通知。</summary>
        public long? LeaveWebhookId { get; set; }

        /// <summary>驳回策略（WfRejectStrategy）：0=驳回发起人；1=驳回到上一节点；2=驳回到指定节点</summary>
        public int RejectStrategy { get; set; }

        /// <summary>驳回目标节点（RejectStrategy=2 时生效）</summary>
        public long? RejectTargetNodeId { get; set; }

        /// <summary>空审批人兜底策略（WfEmptyApproverStrategy）：0=自动通过（默认）；1=指定默认审批人</summary>
        public int EmptyApproverStrategy { get; set; }

        /// <summary>兜底默认审批人（EmptyApproverStrategy=1 时生效）userId</summary>
        public long? DefaultApproverId { get; set; }
    }
}
