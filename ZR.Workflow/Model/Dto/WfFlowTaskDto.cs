namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 审批任务查询对象
    /// </summary>
    public class WfFlowTaskQueryDto : PagerInfo
    {
        /// <summary>申请标题</summary>
        public string Title { get; set; }
        /// <summary>实例Id</summary>
        public long? InstanceId { get; set; }
        /// <summary>任务状态</summary>
        public int? Status { get; set; }
    }

    /// <summary>
    /// 审批任务输入输出对象（含实例冗余字段用于列表展示）
    /// </summary>
    public class WfFlowTaskDto : SysBase
    {
        /// <summary>任务Id</summary>
        public long TaskId { get; set; }

        /// <summary>实例Id</summary>
        public long InstanceId { get; set; }

        /// <summary>节点Id</summary>
        public long NodeId { get; set; }

        /// <summary>节点名称</summary>
        public string NodeName { get; set; }

        /// <summary>审批人</summary>
        public string Assignee { get; set; }

        /// <summary>审批人昵称（快照）</summary>
        public string AssigneeNickName { get; set; }

        /// <summary>审批人 userId（稳定标识，用于减签等按 userId 识别）</summary>
        public long? AssigneeId { get; set; }

        /// <summary>委托代审人Id（任务被委托时填代审人 userId，任务仍归属 AssigneeId）</summary>
        public long? DelegateId { get; set; }

        /// <summary>委托代审人昵称（快照）</summary>
        public string DelegateName { get; set; }

        /// <summary>任务状态</summary>
        public int Status { get; set; } = 0;

        /// <summary>审批意见</summary>
        public string Opinion { get; set; }

        /// <summary>实际动作</summary>
        public int? Action { get; set; }

        /// <summary>任务类型 0=审批 1=抄送</summary>
        public int TaskType { get; set; }

        /// <summary>是否已读</summary>
        public bool IsRead { get; set; }

        /// <summary>处理时间</summary>
        public DateTime? HandleTime { get; set; }

        /// <summary>申请标题（冗余）</summary>
        public string Title { get; set; }

        /// <summary>申请人（冗余）</summary>
        public string ApplyUser { get; set; }

        /// <summary>申请人昵称（快照，冗余）</summary>
        public string ApplyNickName { get; set; }

        /// <summary>流程名称（冗余）</summary>
        public string FlowName { get; set; }

        /// <summary>实例状态（冗余）</summary>
        public int InstanceStatus { get; set; }
    }

    /// <summary>
    /// 审批动作入参
    /// </summary>
    public class WfApproveInput
    {
        /// <summary>任务Id</summary>
        [Required(ErrorMessage = "任务Id不能为空")]
        public long TaskId { get; set; }

        /// <summary>审批意见</summary>
        public string Opinion { get; set; }

        /// <summary>审批人修改后的表单（JSON，可选）。仅提交当前节点有权编辑的字段变更；
        /// 引擎会按节点 FieldPermission 校验：无权字段若被修改则拒绝。为空表示审批人不改表单。</summary>
        public string FormContent { get; set; }
    }

    /// <summary>
    /// 批量审批入参（逐条复用 Approve 流转，仅支持通过）
    /// </summary>
    public class WfBatchApproveInput
    {
        /// <summary>任务Id列表（逗号分隔）</summary>
        public string TaskIds { get; set; }

        /// <summary>统一审批意见（可选）</summary>
        public string Opinion { get; set; }
    }
}
