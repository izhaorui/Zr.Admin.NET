using ZR.Model.System;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 审批任务（每个节点的待办）
    /// </summary>
    [SugarTable("wf_flow_task", "审批任务")]
    public class WfFlowTask : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long TaskId { get; set; }

        /// <summary>
        /// 流程实例Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long InstanceId { get; set; }

        /// <summary>
        /// 节点Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long NodeId { get; set; }

        /// <summary>
        /// 节点名称（冗余）
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string NodeName { get; set; }

        /// <summary>
        /// 审批人（登录名，权限/关联用）
        /// </summary>
        [SugarColumn(Length = 500, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Assignee { get; set; }

        /// <summary>审批人Id（稳定外键）</summary>
        [SugarColumn(IsNullable = true)]
        public long? AssigneeId { get; set; }

        /// <summary>审批人昵称（快照）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string AssigneeNickName { get; set; }

        /// <summary>
        /// 委托代审人Id（稳定外键）。委托生效后填入实际代审人 userId，任务仍归属原审批人（AssigneeId 不变），
        /// 代审人凭此字段可在待办看到并代为审批；未委托时为 null。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? DelegateId { get; set; }

        /// <summary>委托代审人昵称（快照，方便前端直接展示"代 X 审批"）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string DelegateName { get; set; }

        /// <summary>
        /// 任务状态 0=待审 1=已审 2=跳过
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; } = 0;

        /// <summary>
        /// 审批意见
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string Opinion { get; set; }

        /// <summary>
        /// 实际动作 1=通过 2=驳回
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? Action { get; set; }

        /// <summary>
        /// 任务类型 0=审批 1=抄送（区分审批待办与抄送知会；原仅靠 Status=Skipped 易与“被跳过的审批”混淆）
        /// </summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public int TaskType { get; set; } = 0;

        /// <summary>是否已读（待办已读标记，默认未读）</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// 处理时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? HandleTime { get; set; }

        /// <summary>
        /// 待办到达时间（引擎生成待办时写入），用于超时判定与展示。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? ArriveTime { get; set; }

        /// <summary>
        /// 超时截止时间。仅当所属节点 TimeoutHours>0 时由引擎计算 = ArriveTime + TimeoutHours；否则为 null。
        /// 定时任务扫描 Status=Pending 且 DeadlineTime&lt;now 的待办进行超时自动处理。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? DeadlineTime { get; set; }
    }
}
