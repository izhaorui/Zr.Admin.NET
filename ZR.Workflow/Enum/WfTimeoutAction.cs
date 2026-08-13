namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 节点超时动作：待办超过 DeadlineTime 后，由定时任务按此策略自动处理。
    /// </summary>
    public enum WfTimeoutAction
    {
        /// <summary>不处理（默认）：节点未配置超时动作，定时任务跳过该待办。</summary>
        None = 0,
        /// <summary>自动通过：超时后视为审批通过，opinion 标注"超时自动通过"。</summary>
        AutoApprove = 1,
        /// <summary>自动驳回：超时后视为驳回，opinion 标注"超时自动驳回"。</summary>
        AutoReject = 2,
        /// <summary>转交指定人：超时后把待办转给节点配置的 TimeoutTransferUserId 接手；目标为空则退化为自动通过。</summary>
        Transfer = 3
    }
}
