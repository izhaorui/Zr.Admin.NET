namespace ZR.Mall.Enum
{
    /// <summary>
    /// 支付流水状态
    /// </summary>
    public enum PaymentStatusEnum
    {
        /// <summary>
        /// 预支付（已调起/下单，待用户支付）
        /// </summary>
        Prepay = 0,
        /// <summary>
        /// 支付成功
        /// </summary>
        Paid = 1,
        /// <summary>
        /// 已关闭（超时未付/订单取消）
        /// </summary>
        Closed = 2,
        /// <summary>
        /// 已退款
        /// </summary>
        Refunded = 3
    }
}
