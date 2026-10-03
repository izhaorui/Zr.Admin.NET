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

        /// <summary>
        /// 命中的运费模板 Id（下单时快照）
        /// </summary>
        /// <remarks>为 null 表示该行未绑定模板且无默认模板（计 0 运费）</remarks>
        public long? FreightTemplateId { get; set; }

        /// <summary>
        /// 运费模板名称（下单时快照的冗余）
        /// </summary>
        /// <remarks>模板被改名或删除后，订单详情仍可读出"当时按哪个模板计的"</remarks>
        public string FreightTemplateName { get; set; }

        /// <summary>
        /// 买家填写的表单值快照（该行商品配了下单表单时才有值）。
        /// 由实体 OMSOrderItem.FormValues 按属性名映射而来；C 端订单详情/列表靠它回显填写内容。
        /// </summary>
        [ExcelColumn(Ignore = true)]
        public List<OrderFormValue> FormValues { get; set; }
    }
}
