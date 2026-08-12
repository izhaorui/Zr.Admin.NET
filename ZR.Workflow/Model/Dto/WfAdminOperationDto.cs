namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 管理员运维操作通用入参（终止 / 挂起 / 恢复），仅需填写操作原因
    /// </summary>
    public class WfAdminOpinionDto
    {
        /// <summary>操作原因 / 审批意见（可选）</summary>
        public string Opinion { get; set; }
    }

    /// <summary>
    /// 管理员改派入参
    /// </summary>
    public class WfAdminReassignDto
    {
        /// <summary>目标节点（实例当前或任意未完成任务所属节点）</summary>
        public long NodeId { get; set; }

        /// <summary>改派目标用户 userId</summary>
        public long TargetUserId { get; set; }

        /// <summary>改派说明（可选）</summary>
        public string Opinion { get; set; }
    }

    /// <summary>
    /// 管理员跳转节点入参
    /// </summary>
    public class WfAdminJumpDto
    {
        /// <summary>跳转目标节点（必须存在于该流程且非结束节点）</summary>
        public long TargetNodeId { get; set; }

        /// <summary>跳转说明（可选）</summary>
        public string Opinion { get; set; }
    }
}
