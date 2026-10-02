using ZR.Mall.Enum;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 订单管理
    /// </summary>
    [SugarTable("oms_order", "订单表")]
    [SugarIndex("index_orderNo", nameof(OrderNo), OrderByType.Asc, true)]
    [SugarIndex("idx_oms_order_member", nameof(MemberId), OrderByType.Asc, false)]
    [Tenant("MallDb")]
    public class OMSOrder
    {
        /// <summary>
        /// Id 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 订单号 
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 下单手机号快照（= 收货手机号）。⚠️ **非身份凭据**，订单归属只认 MemberId（见 OrderOwnership）。
        /// 与 AddressSnapshot.Phone 同源；单独成列仅为后台订单日志/展示取号方便。
        /// </summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string GuestPhone { get; set; }

        /// <summary>
        /// 会员ID（C 端会员下单时绑定，用于"我的订单"按会员身份查询）。
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDescription = "会员ID", ColumnName = "userId")]
        public long? MemberId { get; set; }

        /// <summary>
        /// 赠送用户ID
        /// </summary>
        public long? ToUserId { get; set; }

        // ── 金额模型（对齐主流开源商城口径：total/freight/discount/integral/pay/refund）──
        // 公式：PayAmount = TotalAmount + FreightAmount - DiscountAmount - IntegralAmount
        // 其中四个附加列本期恒为 0（对应能力尚未上线），此时 PayAmount == TotalAmount。
        // 先把列铺齐，等运费模板 / 优惠券 / 积分 / 售后单上线时直接取用，无需再改表。

        /// <summary>
        /// 商品总额（= Σ 订单项 单价×数量）。**不含**运费、优惠与积分抵扣，是销售额统计的唯口径。
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// 运费。运费模板尚未上线，本期恒为 0；上线后由运费模板按收货地区 code 计算。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public decimal FreightAmount { get; set; }

        /// <summary>
        /// 优惠合计（优惠券 / 满减 / 促销折扣，可叠加）。优惠券尚未上线，本期恒为 0。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// 积分抵扣金额。会员积分尚未上线，本期恒为 0。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public decimal IntegralAmount { get; set; }

        /// <summary>
        /// 实付应付 = 商品总额 + 运费 − 优惠 − 积分抵扣。
        /// 支付预下单、回调金额一致性校验、支付流水金额、退款金额均以本字段为准。
        /// </summary>
        public decimal PayAmount { get; set; }

        /// <summary>
        /// 已退金额累计。恒 ≤ PayAmount，是部分退款 / 重复退款的超额护栏；售后单尚未上线，本期恒为 0。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public decimal RefundAmount { get; set; }

        /// <summary>
        /// 订单状态 0待付款 1待发货 2已发货 3已完成 4已取消 5已退款
        /// </summary>
        public OrderStatusEnum OrderStatus { get; set; }
        /// <summary>
        /// 发货状态：0->未发货；1->已发货；2->已收货；
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public DeliveryStatusEnum DeliveryStatus { get; set; }

        /// <summary>
        /// 退款状态：0->无退款；1->退款中；2->已退款；3->正在退货；4->已退货
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public RefundStatusEnum RefundStatus { get; set; }

        /// <summary>
        /// 下单时间 
        /// </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>
        /// 支付时间 
        /// </summary>
        public DateTime? PayTime { get; set; }

        /// <summary>
        /// 支付类型：0->模拟支付；1->微信H5；2->支付宝（预留）。
        /// 区分真实付款与开发联调的模拟单，用于对账与排查。默认 0。
        /// </summary>
        [SugarColumn(DefaultValue = "0", IsNullable = true)]
        public int? PayType { get; set; }

        /// <summary>
        /// 第三方支付流水号（微信 transaction_id；模拟支付为空）。
        /// 微信退款/对账需凭此号，回调时写入。
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TransactionId { get; set; }

        /// <summary>
        /// 取消时间 
        /// </summary>
        public DateTime? CancelTime { get; set; }

        /// <summary>
        /// 发货时间
        /// </summary>
        public DateTime? ShipTime { get; set; }

        /// <summary>
        /// 确认收货时间
        /// </summary>
        public DateTime? ConfirmTime { get; set; }

        /// <summary>
        /// 订单备注(用户)
        /// </summary>
        public string OrderNote { get; set; }

        /// <summary>
        /// 商家备注 
        /// </summary>
        public string MerchantNote { get; set; }

        /// <summary>
        /// 收货地址 
        /// </summary>
        [SugarColumn(IsJson = true)]
        public AddressSnapshot AddressSnapshot { get; set; }

        /// <summary>
        /// 物流公司 
        /// </summary>
        public string DeliveryCompany { get; set; }

        /// <summary>
        /// 物流单号 
        /// </summary>
        public string DeliveryNo { get; set; }

        /// <summary>
        /// 是否删除(软删除) 1.已删除 0.未删除
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDelete { get; set; }

        /// <summary>
        /// 订单项
        /// </summary>
        [Navigate(NavigateType.OneToMany, nameof(OMSOrderItem.OrderId))]
        public List<OMSOrderItem> Items { get; set; }
    }

    public class AddressSnapshot
    {
        public string UserName { get; set; }
        public string Phone { get; set; }
        public string Province { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string DetailAddress { get; set; }
    }
}