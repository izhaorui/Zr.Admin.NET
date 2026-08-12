namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 节点驳回策略：决定审批人驳回时流程如何回退（落于 WfFlowNode.RejectStrategy）。
    /// </summary>
    public enum WfRejectStrategy
    {
        /// <summary>驳回发起人：流程结束，回到申请人重新编辑提交（默认，兼容性最强）</summary>
        ToApplicant = 0,
        /// <summary>驳回到上一审批节点：回到当前节点的上一个 Audit 节点重新审批，流程保持审批中</summary>
        ToPrevNode = 1,
        /// <summary>驳回到指定节点：回到 RejectTargetNodeId 指向的节点重新审批</summary>
        ToSpecifiedNode = 2
    }
}
