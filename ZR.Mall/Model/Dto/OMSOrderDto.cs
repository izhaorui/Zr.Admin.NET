
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
        /// 买家填写的表单值（该单所有订单项合并，脱敏后拼成一行文本）。
        ///
        /// 只用于**导出**：列表不返回（批量带明文风险高），详情走订单项上的 FormValues 结构化返回。
        /// 值由 <see cref="Common.OrderFormValueHelper.ToText"/> 生成，形如「接收手机号：138****8000；接收账号：abc123」。
        /// </summary>
        [ExcelColumn(Name = "买家填写信息")]
        public string FormValuesText { get; set; }

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
    /// 销售总额与订单数（<c>OMSOrderService.GetTotalSales</c>）。
    /// 口径：商品总额（不含运费），与 <see cref="Common.MallStatsScope"/> 一致。
    /// </summary>
    public class SalesTotalDto
    {
        /// <summary>销售总额（商品口径）</summary>
        public decimal TotalSales { get; set; }

        /// <summary>订单数</summary>
        public int OrderCount { get; set; }
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
    /// 经营概览（销售统计页顶部体温区）。
    ///
    /// 口径全部与销售总额/趋势/Top10 保持一致：<b>已支付且未作废</b>的订单
    /// （IsDelete=0、PayTime 非空、OrderStatus ∉ {已取消, 已退款}）。
    /// 口径不统一时"退款率 > 100%"这种荒唐数字就会出现，故口径只在
    /// <see cref="Common.MallStatsScope"/> 一处定义，各统计方法都从那里取。
    /// </summary>
    /// <remarks>
    /// 比例类字段都由服务端算好（<c>Rate</c> 系列）：让前端自己拿两个数相除，
    /// 会出现"分母为 0 时显示 NaN/Infinity"以及各处四舍五入不一致。
    /// </remarks>
    public class BusinessOverviewDto
    {
        /// <summary>有效订单数（已支付且未作废）</summary>
        public int OrderCount { get; set; }

        /// <summary>
        /// 销售额（商品口径 Σ TotalAmount，运费不计入）—— 与销售总额接口同口径
        /// </summary>
        public decimal TotalSales { get; set; }

        /// <summary>实收金额（Σ PayAmount，含运费）</summary>
        public decimal TotalPaid { get; set; }

        /// <summary>客单价 = 实收 / 有效订单数；无订单时为 0</summary>
        public decimal AvgOrderAmount { get; set; }

        /// <summary>发生退款（含部分退款）的订单数</summary>
        public int RefundOrderCount { get; set; }

        /// <summary>退款金额合计（Σ 订单的 RefundAmount，不是售后单的申请额）</summary>
        public decimal RefundAmount { get; set; }

        /// <summary>
        /// 金额退款率 = 退款金额 / 实收金额（小数，0~1）。
        /// 比"退款单数占比"更贴近真实：退 1 元的单和退 500 元的单不该同权。
        /// </summary>
        public decimal RefundAmountRate { get; set; }

        /// <summary>单量退款率 = 退款订单数 / 有效订单数（小数，0~1）</summary>
        public decimal RefundOrderRate { get; set; }

        /// <summary>售后单数（含全部状态，按申请时间落在区间内统计）</summary>
        public int AfterSaleCount { get; set; }

        /// <summary>售后率 = 售后单数 / 有效订单数（小数，0~1）</summary>
        /// <remarks>
        /// 一个订单可能产生多笔售后（历史上同订单只限制"同时一笔进行中"），
        /// 故该值可能大于 100% —— 这是口径事实，不做人为收敛。
        /// </remarks>
        public decimal AfterSaleRate { get; set; }

        /// <summary>实物订单数（OrderType=1）</summary>
        public int PhysicalOrderCount { get; set; }

        /// <summary>虚拟订单数（OrderType=2）</summary>
        public int VirtualOrderCount { get; set; }

        /// <summary>虚拟订单占比（小数，0~1）</summary>
        public decimal VirtualOrderRate { get; set; }

        /// <summary>下单会员数（MemberId 非空去重）</summary>
        public int MemberOrderCount { get; set; }

        /// <summary>
        /// 复购率 = 下单 ≥2 次的会员数 / 下单会员数（小数，0~1）。
        /// 反映"有没有回头客"，比客单价更能说明商城健康度。
        /// </summary>
        public decimal RepurchaseRate { get; set; }
    }

    /// <summary>
    /// 退款 Top10 商品（销售统计页）：判断"要不要下架某个商品"的依据。
    ///
    /// 口径诚实说明：退款金额是**订单级**字段，订单项上没有退款额，
    /// 所以"某商品退了多少"在部分退款场景无法精确拆分到商品。
    /// 故 <see cref="RefundSales"/> 是"退款订单里该商品的销售额"（规模参考），
    /// 真正的退款金额请看经营概览里的总额口径。
    /// </summary>
    public class RefundTopProductDto
    {
        public long ProductId { get; set; }

        /// <summary>商品名（商品已删除时为空）</summary>
        public string ProductName { get; set; }

        /// <summary>该商品出现在多少笔退款订单里（按订单+商品去重，不会被多规格行算重）</summary>
        public int RefundOrderCount { get; set; }

        /// <summary>退款订单里该商品的销售额合计（⚠️ 不是退款金额，字段名刻意不叫 RefundAmount）</summary>
        public decimal RefundSales { get; set; }

        /// <summary>该商品在区间内的总销售额（分母，有效订单口径）</summary>
        public decimal TotalSales { get; set; }

        /// <summary>退款销售额占比 = RefundSales / TotalSales（小数，0~1）</summary>
        public decimal RefundRate { get; set; }
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

    /// <summary>
    /// 待发货订单**导出**专用行（打单用）。
    ///
    /// 与 <see cref="DeliveryExpressDto"/>（导入模板）刻意分开：导入模板的列是运营既有习惯，
    /// 加列会打乱他们保存的 Excel；而导出本来就该给足信息——原先只导出订单号/快递/单号三列，
    /// 打单软件拿不到收件人与商品明细，等于导出了也没法用。
    /// </summary>
    /// <remarks>
    /// 收货人/手机号**明文不脱敏**：这份文件的用途就是打单，真实号码是业务必需
    /// （脱敏后打单软件直接不可用，只能人工对照手输）。
    /// 风险由使用纪律承担：文件会落到本地磁盘/被转发，故仅在**导出**这一出口提供，不进日志、不进订单详情。
    /// </remarks>
    public class DeliveryExportDto
    {
        [ExcelColumn(Name = "订单号", Width = 30)]
        [ExcelColumnName("订单号")]
        public string OrderNo { get; set; }

        [ExcelColumn(Name = "收件人", Width = 16)]
        [ExcelColumnName("收件人")]
        public string ReceiverName { get; set; }

        [ExcelColumn(Name = "收件手机号", Width = 18)]
        [ExcelColumnName("收件手机号")]
        public string ReceiverPhone { get; set; }

        [ExcelColumn(Name = "收货地区", Width = 24)]
        [ExcelColumnName("收货地区")]
        public string ReceiverRegion { get; set; }

        [ExcelColumn(Name = "详细地址", Width = 40)]
        [ExcelColumnName("详细地址")]
        public string DetailAddress { get; set; }

        [ExcelColumn(Name = "商品明细", Width = 50)]
        [ExcelColumnName("商品明细")]
        public string GoodsSummary { get; set; }

        [ExcelColumn(Name = "物流公司", Width = 30)]
        [ExcelColumnName("物流公司")]
        public string DeliveryCompany { get; set; }

        [ExcelColumn(Name = "物流单号", Width = 30)]
        [ExcelColumnName("物流单号")]
        public string DeliveryNo { get; set; }
    }
}