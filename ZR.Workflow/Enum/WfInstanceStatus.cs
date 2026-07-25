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
        Withdrawn = 3
    }
}
