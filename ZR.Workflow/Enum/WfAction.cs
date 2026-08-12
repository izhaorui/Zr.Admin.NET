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
        Withdraw = 4,
        /// <summary>加签</summary>
        AddSign = 5,
        /// <summary>重新提交</summary>
        Resubmit = 6,
        /// <summary>抄送</summary>
        Cc = 7,
        /// <summary>自动跳过（审批人为空时节点自动通过，仅留痕）</summary>
        AutoSkip = 8,
        /// <summary>减签（移除本节点某审批人，任务置 Skipped 并重新判定节点完成）</summary>
        RemoveSign = 9,
        /// <summary>委托代审（原审批人将待办委托给他人代审，任务仍归属原审批人）</summary>
        Delegate = 10
    }
}
