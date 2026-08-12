namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 流程实例状态
    /// </summary>
    public enum WfInstanceStatus
    {
        /// <summary>审批中</summary>
        Approval = 0,
        /// <summary>通过</summary>
        Approved = 1,
        /// <summary>驳回</summary>
        Rejected = 2,
        /// <summary>撤回</summary>
        Withdrawn = 3,
        /// <summary>已挂起（管理员暂停，等待恢复）</summary>
        Suspended = 4,
        /// <summary>已终止/作废（管理员强制结束，不可逆）</summary>
        Terminated = 5
    }
}
