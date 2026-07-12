namespace ZR.Mall.Model
{
    /// <summary>
    /// 订单项
    /// </summary>
    [SugarTable("oms_order_item", "订单项")]
    [Tenant("MallDb")]
    public class OMSOrderItem
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long ItemId { get; set; }
        /// <summary>
        /// 订单ID
        /// </summary>
        public long OrderId { get; set; }
        /// <summary>
        /// 商品ID(快照)
        /// </summary>
        public long ProductId { get; set; }
        /// <summary>
        /// 商品名(快照)
        /// </summary>
        public string ProductName { get; set; }
        /// <summary>
        /// 商品图片(快照)
        /// </summary>
        public string ProductPic { get; set; }
        /// <summary>
        /// skuId
        /// </summary>
        public long SkuId { get; set; }
        /// <summary>
        /// 商品的单价快照（下单时的价格）（快照）
        /// </summary>
        public decimal UnitPrice  { get; set; }
        /// <summary>
        /// 付款金额(单价UnitPrice*数量Quantity)
        /// </summary>
        public decimal TotalPrice { get; set; }
        /// <summary>
        /// 数量
        /// </summary>
        public int Quantity { get; set; }
        /// <summary>
        /// 规格快照(例如:颜色:红色;尺码:XL)
        /// </summary>
        public string SkuSpec { get; set; }
        public DateTime AddTime { get; set; }
    }
}
