namespace ZR.Mall.Model
{
    /// <summary>
    /// 订单项
    /// </summary>
    public class OMSOrderItemDto
    {
        public long ItemId { get; set; }
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
        public long SkuId { get; set; }
        /// <summary>
        /// 下单时的商品单价快照。
        /// 注意：属性名必须与实体 OMSOrderItem.UnitPrice 保持一致 —— 实体与 DTO 由 Mapster
        /// 按属性名映射，早期命名为 Price 时匹配不到源属性，导致接口下发的单价恒为 0。
        /// </summary>
        public decimal UnitPrice { get; set; }
        /// <summary>
        /// 总价(单价 * 数量)
        /// </summary>
        public decimal TotalPrice { get; set; }
        public int Quantity { get; set; }
        /// <summary>
        /// 规格快照(例如:颜色:红色;尺码:XL)
        /// </summary>
        public string SkuSpec { get; set; }
    }
}
