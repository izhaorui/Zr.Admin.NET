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
        Delegate = 10,
        /// <summary>挂起（管理员暂停流程）</summary>
        Suspend = 11,
        /// <summary>恢复（管理员恢复被挂起的流程）</summary>
        Resume = 12,
        /// <summary>终止/作废（管理员强制结束流程，不可逆）</summary>
        Terminate = 13,
        /// <summary>改派（管理员把某节点待办改给其他人）</summary>
        Reassign = 14,
        /// <summary>跳转节点（管理员把卡住的实例跳到指定节点）</summary>
        Jump = 15,
        /// <summary>催办（申请人对在审批中的实例主动提醒当前节点审批人）</summary>
        Urge = 16
    }
}
