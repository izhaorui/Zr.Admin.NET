namespace ZR.Workflow.Model
{
    /// <summary>
    /// 审批记录（流水轨迹）
    /// </summary>
    [SugarTable("wf_flow_record", "审批记录")]
    public class WfFlowRecord : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long RecordId { get; set; }

        /// <summary>
        /// 关联任务Id
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? TaskId { get; set; }

        /// <summary>
        /// 流程实例Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long InstanceId { get; set; }

        /// <summary>
        /// 节点Id
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? NodeId { get; set; }

        /// <summary>
        /// 操作人（登录名，权限/关联用）
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Operator { get; set; }

        /// <summary>操作人Id（稳定外键）</summary>
        [SugarColumn(IsNullable = true)]
        public long? OperatorId { get; set; }

        /// <summary>操作人昵称（快照）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string OperatorNickName { get; set; }

        /// <summary>
        /// 动作，取值以 WfAction 枚举为准：
        /// 0=提交 1=通过 2=驳回 3=转交 4=撤回 5=加签 6=重新提交 7=抄送
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Action { get; set; } = 0;

        /// <summary>
        /// 审批意见
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string Opinion { get; set; }

        /// <summary>是否已读（抄送/记录已读标记，默认未读）</summary>
        [SugarColumn(IsNullable = false, DefaultValue = "0")]
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// AI 自动生成的审批摘要（提交后异步生成，可空）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string Summary { get; set; }
    }
}
