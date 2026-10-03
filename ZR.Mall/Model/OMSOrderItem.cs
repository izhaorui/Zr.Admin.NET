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

        /// <summary>
        /// 命中的运费模板 Id（下单时快照）
        /// </summary>
        /// <remarks>
        /// 留痕用：订单运费是"按模板分段求和"的结果，事后要能反查是哪几个模板、当时各是什么规则。
        /// 为 null 表示该行商品未绑定模板且无默认模板（计 0 运费）。
        /// </remarks>
        public long? FreightTemplateId { get; set; }

        /// <summary>
        /// 运费模板名称（下单时快照的冗余）
        /// </summary>
        /// <remarks>
        /// 与 <see cref="ProductName"/> 同一思路：模板被改名或删除后，订单详情仍可读出
        /// "当时按哪个模板计的"，不必依赖模板还存在。
        /// </remarks>
        [SugarColumn(Length = 50)]
        public string FreightTemplateName { get; set; }

        /// <summary>
        /// 买家填写的表单值快照（JSON 列）。为 null/空表示该行商品没有配下单表单。
        /// </summary>
        /// <remarks>
        /// 存快照而不是回查商品当前配置：订单是历史凭证，商品改字段名/删字段后，
        /// 旧订单仍要能读出"当时填了什么、那个字段叫什么"（与价格/规格快照同理）。
        /// 列类型显式指定为长文本：多字段 + 长文本域的组合会超出短列上限。
        /// </remarks>
        [SugarColumn(IsJson = true, ColumnDataType = StaticConfig.CodeFirst_BigString, ColumnDescription = "表单填写值快照")]
        public List<OrderFormValue> FormValues { get; set; }

        public DateTime AddTime { get; set; }
    }

    /// <summary>
    /// 订单项上的表单填写值快照（存 JSON 列，见 <see cref="OMSOrderItem.FormValues"/>）。
    /// 连 Label/Type 一起存：展示时只读订单即可，不必回查商品是否改过表单。
    /// </summary>
    public class OrderFormValue
    {
        /// <summary>字段键（对应下单时商品表单定义的 Field）</summary>
        public string Field { get; set; }

        /// <summary>字段显示名（下单那一刻的文案）</summary>
        public string Label { get; set; }

        /// <summary>字段类型（下单那一刻的类型）</summary>
        public string Type { get; set; }

        /// <summary>买家填写的值</summary>
        public string Value { get; set; }
    }
}
