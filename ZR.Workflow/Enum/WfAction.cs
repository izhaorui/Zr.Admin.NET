namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 审批动作
    /// </summary>
    public enum WfAction
    {
        /// <summary>提交</summary>
        Submit = 0,
        /// <summary>通过</summary>
        Approve = 1,
        /// <summary>驳回</summary>
        Reject = 2,
        /// <summary>转交</summary>
        Transfer = 3,
        /// <summary>撤回</summary>
        Withdraw = 4
    }
}
