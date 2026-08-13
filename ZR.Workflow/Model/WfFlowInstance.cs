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
        /// 申请人（登录名，权限/关联用）
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string ApplyUser { get; set; }

        /// <summary>申请人Id（稳定外键，按此查询/关联，避免依赖可变登录名）</summary>
        [SugarColumn(IsNullable = true)]
        public long? ApplyUserId { get; set; }

        /// <summary>申请人昵称（提交时快照，展示用，免运行时关联用户表）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string ApplyNickName { get; set; }

        /// <summary>
        /// 实例状态 0=审批中 1=通过 2=驳回 3=撤回
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; } = 0;

        /// <summary>
        /// 当前节点Id（单值兼容字段：无并行活动时等于唯一活动节点；并行期间等于首个活动节点，仅用于兼容旧查询/列表展示）
        /// 真正的并行活动节点集合见 <see cref="CurrentNodeIds"/>。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? CurrentNodeId { get; set; }

        /// <summary>
        /// 当前活动节点集合（JSON 数组，如 [12,15,18]）。并行网关节点(7) fork 后此处会有多个节点；
        /// 单分支流转时退化为只含 1 个元素的数组，与 <see cref="CurrentNodeId"/> 保持一致。
        /// 流程结束/未开始为 null 或空数组。
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string CurrentNodeIds { get; set; }

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

        /// <summary>
        /// 最近一次催办时间。用于申请人催办的 24 小时限频：距上次催办不足 24h 则拒绝再次催办。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? LastUrgeTime { get; set; }
    }
}
