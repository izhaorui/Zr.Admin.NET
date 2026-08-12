namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 空审批人兜底策略：当审批节点 ResolveApprovers 解析出的实际审批人为空时如何处理。
    /// </summary>
    public enum WfEmptyApproverStrategy
    {
        /// <summary>自动通过：节点视为无需审批，自动跳过（默认）。</summary>
        AutoPass = 0,
        /// <summary>指定默认审批人：退回到节点配置的 DefaultApproverId 代为审批；若仍为空则退化为自动通过。</summary>
        DefaultUser = 1
    }
}
