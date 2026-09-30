namespace ZR.Mall.Enum
{
    /// <summary>
    /// 后台修改订单的操作类型（对应 OMSOrderDto.OperType 与 IOMSOrderService.UpdateOMSOrder）。
    /// 用枚举替代原先散在实现里的魔法数字 2/3/4，避免误传值时静默走通用更新把整单字段改掉。
    /// </summary>
    public enum OrderUpdateOperType
    {
        /// <summary>
        /// 通用更新：按传入实体整体更新非空字段。
        /// 注意：真正的"发货"请走 OrderDelivery（会释放锁定库存），不要用它替代。
        /// </summary>
        General = 1,

        /// <summary>
        /// 修改平台备注（MerchantNote）
        /// </summary>
        MerchantNote = 2,

        /// <summary>
        /// 修改收货地址（AddressSnapshot）
        /// </summary>
        Address = 3,

        /// <summary>
        /// 退款：关闭订单并回补库存（委托状态内核 RefundOrder）
        /// </summary>
        Refund = 4
    }
}
