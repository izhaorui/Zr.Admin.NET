namespace ZR.Mall.Model
{
    /// <summary>
    /// 售后单（仅退款 / 退货退款）
    /// </summary>
    /// <remarks>
    /// 单独建单而不复用订单退款：复用会把已完成订单改成已关闭，丢掉「曾经完成」这一事实。
    /// 金额口径、库存回补与超额护栏见 OMSOrderStateService.ApplyAfterSaleRefund。
    /// </remarks>
    [SugarTable("oms_after_sale", "售后单")]
    // 售后单号唯一：对外沟通与渠道对账都靠它
    [SugarIndex("idx_oms_after_sale_no", nameof(AfterSaleNo), OrderByType.Asc, true)]
    // 订单详情要按订单查售后记录
    [SugarIndex("idx_oms_after_sale_order", nameof(OrderId), OrderByType.Asc, false)]
    // C 端「我的售后」按会员查
    [SugarIndex("idx_oms_after_sale_member", nameof(MemberId), OrderByType.Asc, false)]
    [Tenant("MallDb")]
    public class OMSAfterSale
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>售后单号（对外沟通与渠道对账用）</summary>
        [SugarColumn(Length = 64)]
        public string AfterSaleNo { get; set; }

        /// <summary>订单 Id</summary>
        public long OrderId { get; set; }

        /// <summary>订单号（冗余，便于列表直接展示与排查）</summary>
        [SugarColumn(Length = 64)]
        public string OrderNo { get; set; }

        /// <summary>会员 Id</summary>
        public long? MemberId { get; set; }

        /// <summary>售后类型：1 仅退款 / 2 退货退款，见 <see cref="Enum.AfterSaleTypeEnum"/></summary>
        public int Type { get; set; }

        /// <summary>状态，见 <see cref="Enum.AfterSaleStatusEnum"/></summary>
        public int Status { get; set; }

        /// <summary>买家申请的退款金额（元）</summary>
        [SugarColumn(DecimalDigits = 2)]
        public decimal ApplyAmount { get; set; }

        /// <summary>实际登记的退款金额（元）；未登记时为 0。订单累计退款额以它为准</summary>
        [SugarColumn(DecimalDigits = 2)]
        public decimal RefundAmount { get; set; }

        /// <summary>申请原因（买家从固定选项里选，如"不想要了""商品破损"）</summary>
        [SugarColumn(Length = 100)]
        public string Reason { get; set; }

        /// <summary>买家补充说明</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string Describe { get; set; }

        /// <summary>申请时间</summary>
        public DateTime ApplyTime { get; set; }

        /// <summary>审核人（后台用户名）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string AuditBy { get; set; }

        /// <summary>审核时间</summary>
        public DateTime? AuditTime { get; set; }

        /// <summary>审核备注（驳回原因必填，通过时可留空）</summary>
        [SugarColumn(Length = 255, IsNullable = true)]
        public string AuditRemark { get; set; }

        /// <summary>退货物流公司（买家填写）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string ReturnCompany { get; set; }

        /// <summary>退货物流单号（买家填写）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string ReturnNo { get; set; }

        /// <summary>买家填写退货物流的时间</summary>
        public DateTime? ReturnTime { get; set; }

        /// <summary>商家确认收到退货的时间</summary>
        public DateTime? ReceiveTime { get; set; }

        /// <summary>收货备注（如"少件""影响二次销售"，供后续追责）</summary>
        [SugarColumn(Length = 255, IsNullable = true)]
        public string ReceiveRemark { get; set; }

        /// <summary>渠道退款单号（微信退款单号等），与渠道流水核对用</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string RefundChannelNo { get; set; }

        /// <summary>登记渠道退款完成的时间</summary>
        public DateTime? RefundTime { get; set; }

        /// <summary>买家撤销售后时间</summary>
        public DateTime? CancelTime { get; set; }

        /// <summary>软删除：1 已删除 / 0 正常</summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDelete { get; set; }
    }
}
