namespace ZR.Mall.Enum
{
    /// <summary>
    /// 售后单状态
    /// </summary>
    /// <remarks>
    /// 两条流程最终都汇到「待退款」，再由运营登记渠道退款后收口：
    /// <code>
    /// 仅退款：  待审核 → 待退款 → 已完成
    /// 退货退款：待审核 → 待买家退货 → 待商家收货 → 待退款 → 已完成
    /// </code>
    /// 任意阶段买家可撤销（→ 已撤销），审核环节可驳回（→ 已驳回）。
    /// </remarks>
    public enum AfterSaleStatusEnum
    {
        /// <summary>待审核</summary>
        Pending = 0,

        /// <summary>已驳回（审核不通过，流程终止）</summary>
        Rejected = 1,

        /// <summary>待买家退货（仅退货退款，审核通过后进入）</summary>
        WaitingReturn = 2,

        /// <summary>待商家收货（买家已填退货物流单号）</summary>
        WaitingReceive = 3,

        /// <summary>待退款（仅退款审核通过 / 退货已由商家确认收到）</summary>
        WaitingRefund = 4,

        /// <summary>已完成（渠道退款已由运营登记）</summary>
        Completed = 5,

        /// <summary>已撤销（买家主动撤销售后）</summary>
        Cancelled = 6,
    }
}
