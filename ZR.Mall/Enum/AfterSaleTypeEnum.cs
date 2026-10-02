namespace ZR.Mall.Enum
{
    /// <summary>
    /// 售后类型
    /// </summary>
    public enum AfterSaleTypeEnum
    {
        /// <summary>仅退款：不寄回商品直接退钱（未发货，或货值低于运费/买家拒收）</summary>
        RefundOnly = 1,

        /// <summary>退货退款：买家先寄回商品，商家确认收到后再退款</summary>
        ReturnRefund = 2,
    }
}
