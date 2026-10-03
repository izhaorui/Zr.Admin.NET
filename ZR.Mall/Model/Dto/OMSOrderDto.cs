
using ZR.Mall.Enum;

namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 订单管理查询对象
    /// </summary>
    public class OMSOrderQueryDto : PagerInfo
    {
        public string OrderNo { get; set; }

        /// <summary>
        /// C 端会员ID（ums_member.Id）。用于后台按会员查订单；不传表示不限。
        /// </summary>
        public long? MemberId { get; set; }

        public OrderStatusEnum? OrderStatus { get; set; }
        public DateTime? BeginCreateTime { get; set; }
        public DateTime? EndCreateTime { get; set; }
        public int? ConfirmStatus { get; set; }
        public string DeliveryNo { get; set; }
    }

    /// <summary>
    /// 订单管理输入输出对象
    /// </summary>
    public class OMSOrderDto
    {
        /// <summary>
        /// 操作类型，取值见 <see cref="OrderUpdateOperType"/>：
        /// 1=通用更新 / 2=修改平台备注 / 3=修改收货地址 / 4=退款。
        /// </summary>
        [ExcelColumn(Ignore = true)]
        public int OperType { get; set; }

        [ExcelColumn(Name = "订单Id")]
        public long Id { get; set; }

        [ExcelColumn(Name = "订单号")]
        public string OrderNo { get; set; }

        /// <summary>
        /// C 端会员ID（ums_member.Id）。商城订单**只认这一种账号身份**，归属判定一律按它
        /// （商城已取消游客下单，新订单必然有值；为 null 只可能是极早期遗留数据）。
        /// </summary>
        [ExcelColumn(Name = "会员ID")]
        public long? MemberId { get; set; }

        /// <summary>
        /// 商品总额（= Σ 订单项 单价×数量），销售额统计口径。
        /// </summary>
        [ExcelColumn(Name = "总金额")]
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// 运费。由运费模板按「收货地区编码 + 商品件数/重量」算出；未绑定模板且无默认模板时为 0。
        /// </summary>
        [ExcelColumn(Name = "运费")]
        public decimal FreightAmount { get; set; }

        /// <summary>
        /// 优惠合计（优惠券/满减/促销折扣）。优惠券未上线，恒为 0。
        /// </summary>
        [ExcelColumn(Name = "优惠")]
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// 积分抵扣金额。会员积分未上线，恒为 0。
        /// </summary>
        [ExcelColumn(Name = "积分抵扣")]
        public decimal IntegralAmount { get; set; }

        /// <summary>
        /// 实付应付 = 商品总额 + 运费 − 优惠 − 积分抵扣。
        /// </summary>
        [ExcelColumn(Name = "付款金额")]
        public decimal PayAmount { get; set; }

        /// <summary>
        /// 已退金额累计（订单页直接退款与售后退款都写这里）；导出保留该列便于对账。
        /// </summary>
        [ExcelColumn(Name = "已退金额")]
        public decimal RefundAmount { get; set; }

        [ExcelColumn(Name = "支付方式")]
        public int? PayType { get; set; }

        [ExcelColumn(Name = "支付流水号")]
        public string TransactionId { get; set; }

        [ExcelColumn(Name = "订单状态", Ignore = true)]
        public int? OrderStatus { get; set; }

        /// <summary>
        /// 订单形态：1 实物 / 2 虚拟，下单时的快照，见 OMSOrder.OrderType。
        /// </summary>
        [ExcelColumn(Name = "订单类型", Ignore = true)]
        public ProductTypeEnum OrderType { get; set; } = ProductTypeEnum.Physical;

        /// <summary>
        /// 是否已超过售后申请期限（服务端按 MallSettings:AfterSaleDays 算好）。只在订单详情回填；
        /// 只回布尔，客户端不自己算时间。
        /// </summary>
        [ExcelColumn(Ignore = true)]
        public bool AfterSaleExpired { get; set; }

        [ExcelColumn(Name = "下单时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? CreateTime { get; set; }

        [ExcelColumn(Name = "支付时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? PayTime { get; set; }

        [ExcelColumn(Name = "取消时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? CancelTime { get; set; }

        [ExcelColumn(Name = "订单备注")]
        public string OrderNote { get; set; }

        [ExcelColumn(Name = "商家备注")]
        public string MerchantNote { get; set; }

        [ExcelColumn(Name = "快递状态")]
        public DeliveryStatusEnum DeliveryStatus { get; set; }

        /// <summary>
        /// 退款状态：0无退款 / 1退款中 / 2已退款 / 3正在退货 / 4已退货。
        /// 取消"已支付"订单会先置为 1（退款中），待运营确认渠道退款完成后转 2（已退款）；
        /// 后台列表据此决定是否展示"确认退款"入口。
        /// </summary>
        [ExcelColumn(Name = "退款状态", Ignore = true)]
        public RefundStatusEnum RefundStatus { get; set; }

        [ExcelColumn(Name = "物流公司")]
        public string DeliveryCompany { get; set; }

        [ExcelColumn(Name = "物流单号")]
        public string DeliveryNo { get; set; }

        [ExcelColumn(Name = "发货时间")]
        public DateTime? ShipTime { get; set; }

        /// <summary>
        /// 确认收货时间：已发货 → 已完成 时写入（见 OMSOrderStateService.ConfirmReceipt）。
        /// 原先 DTO 缺这个字段，导致 C 端订单详情无从展示"什么时候确认收货的"，
        /// 前端只能按状态反推。这里补上后前端才有 <c>order.confirmTime</c> 可用。
        /// </summary>
        [ExcelColumn(Name = "确认收货时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? ConfirmTime { get; set; }

        [ExcelColumn(Name = "订单状态")]
        public string OrderStatusLabel { get; set; }

        [JsonIgnore]
        [ExcelColumn(Name = "收货人", Width = 30)]
        public string User { get; set; }

        [ExcelColumn(Name = "收货地址", Width = 60)]
        public string AddressLabel
        {
            get
            {
                if (AddressSnapshot != null)
                {
                    return $"{AddressSnapshot.Province}/{AddressSnapshot.City}/{AddressSnapshot.DetailAddress}";
                }
                return string.Empty;
            }
        }

        [ExcelColumn(Ignore = true)]
        [SugarColumn(IsJson = true)]
        public AddressSnapshot AddressSnapshot { get; set; }
        [ExcelColumn(Ignore = true)]
        public List<OMSOrderItemDto> Items { get; set; }

        /// <summary>
        /// 表单填写值汇总（行项快照拼成一行文本）：供后台列表快速浏览与订单导出。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="Items"/> 的关系：这里是把每个商品行的表单值摊平成一格文本，
        /// 运营扫一眼就能看到"接收账号填了什么"；要看结构化字段名/类型，订单详情页读
        /// <c>items[].formValues</c>（那是下单那一刻的快照，含 label/type）。
        /// <para>行项内多个字段用「；」隔开，行与行之间用「|」隔开 —— 导出单元格里不能有换行。</para>
        /// <para>不参与 Excel 导出：导出走 <c>OMSOrderService.ExportList</c>，那里刻意不加载行项
        /// （导出是订单级一行一单，页大小上限 10 万，再带明细会把查询与内存一起拖垮），
        /// 带过来只会得到一整列空白，不如不进模板。</para>
        /// </remarks>
        [ExcelColumn(Ignore = true)]
        public string FormValuesLabel
        {
            get
            {
                if (Items == null || Items.Count == 0)
                {
                    return string.Empty;
                }

                var rows = new List<string>();
                foreach (var item in Items)
                {
                    if (item?.FormValues == null || item.FormValues.Count == 0)
                    {
                        continue;
                    }

                    var fields = string.Join("；", item.FormValues
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => $"{x.Label}：{x.Value}"));
                    if (fields.Length == 0)
                    {
                        continue;
                    }
                    rows.Add(string.IsNullOrWhiteSpace(item.ProductName) ? fields : $"{item.ProductName} - {fields}");
                }

                return string.Join(" | ", rows);
            }
        }
    }

    /// <summary>
    /// 订单概览统计（列表顶部概览卡）：各状态计数 + 待发货未发货数。
    /// 由 <c>OMSOrderService.GetOrderOverview</c> 一次 GROUP BY 查询得出。
    /// </summary>
    public class OrderOverviewDto
    {
        /// <summary>各状态订单数，key = <see cref="OrderStatusEnum"/> 值</summary>
        public Dictionary<int, int> StatusStats { get; set; } = new();

        /// <summary>
        /// 待发货且未发货的订单数（比"待发货"总数更严格：已标记发货的待发货单不计入）。
        /// </summary>
        public int NotDelivered { get; set; }
    }

    /// <summary>
    /// 快递发货
    /// </summary>
    public class DeliveryExpressDto
    {
        [ExcelColumn(Name = "订单号", Width = 30)]
        [ExcelColumnName("订单号")]
        public string OrderNo { get; set; }

        [ExcelColumn(Name = "物流公司", Width = 30)]
        [ExcelColumnName("物流公司")]
        public string DeliveryCompany { get; set; }

        [ExcelColumn(Name = "物流单号", Width = 30)]
        [ExcelColumnName("物流单号")]
        public string DeliveryNo { get; set; }

        [ExcelColumn(Ignore = true)]
        public string Status { get; set; }
    }
}