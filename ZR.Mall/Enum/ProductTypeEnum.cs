namespace ZR.Mall.Enum
{
    /// <summary>
    /// 商品形态：1 实物 / 2 虚拟（纯服务类），默认实物。
    /// 虚拟商品的四条差异（免运费、免收货地址、付款即完成、买家不可售后）不另设字段，由形态推导，
    /// 分流点固定在下单 / 支付 / 运费 / 售后四处，不往页面里散形态分支。
    /// </summary>
    public enum ProductTypeEnum
    {
        /// <summary>实物：要收货地址、按运费模板计费、发货后确认收货</summary>
        Physical = 1,

        /// <summary>虚拟（纯服务类）：免运费、免收货地址、付款即完成、买家不可申请售后</summary>
        Virtual = 2,
    }
}
