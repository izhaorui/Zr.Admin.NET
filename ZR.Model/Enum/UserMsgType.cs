
public enum UserMsgType
{
    SYSTEM = 1,
    PRAISE = 2,
    COMMENT = 3,
    /// <summary>
    /// 租户通知：平台对租户管理员推送的租户级系统消息（停服/续费/套餐变更等）
    /// </summary>
    TENANT_NOTICE = 4,
    /// <summary>
    /// 商城订单通知：发货/退款等订单状态变化推送给买家
    /// </summary>
    ORDER = 5,
    /// <summary>
    /// 工作流通知：审批待办/通过/驳回/转办/加签/撤回/重提等流转提醒
    /// </summary>
    WORKFLOW = 6
}
