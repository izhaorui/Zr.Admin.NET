using ZR.Mall.Enum;

namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 售后单查询对象（后台与 C 端共用；C 端由服务端强制按当前会员过滤，不采信传入的 MemberId）
    /// </summary>
    public class AfterSaleQueryDto : PagerInfo
    {
        public string AfterSaleNo { get; set; }
        public string OrderNo { get; set; }
        public AfterSaleStatusEnum? Status { get; set; }
        public AfterSaleTypeEnum? Type { get; set; }
        public long? MemberId { get; set; }
        public DateTime? BeginApplyTime { get; set; }
        public DateTime? EndApplyTime { get; set; }
    }

    /// <summary>
    /// 售后单输出对象
    /// </summary>
    /// <remarks>
    /// 刻意带上订单侧三个字段（实付 / 已退 / 订单状态）：登记退款时要判断"还能退多少"，
    /// 列表里也要能一眼看出上限，否则运营得切到订单页去查。
    /// </remarks>
    public class AfterSaleDto
    {
        public long Id { get; set; }
        public string AfterSaleNo { get; set; }
        public long OrderId { get; set; }
        public string OrderNo { get; set; }
        public long? MemberId { get; set; }
        public AfterSaleTypeEnum Type { get; set; }
        public AfterSaleStatusEnum Status { get; set; }
        /// <summary>买家申请金额（元）</summary>
        public decimal ApplyAmount { get; set; }
        /// <summary>实际登记退款金额（元）；未登记时为 0</summary>
        public decimal RefundAmount { get; set; }
        public string Reason { get; set; }
        public string Describe { get; set; }
        public DateTime ApplyTime { get; set; }
        public string AuditBy { get; set; }
        public DateTime? AuditTime { get; set; }
        public string AuditRemark { get; set; }
        public string ReturnCompany { get; set; }
        public string ReturnNo { get; set; }
        public DateTime? ReturnTime { get; set; }
        public DateTime? ReceiveTime { get; set; }
        public string ReceiveRemark { get; set; }
        public string RefundChannelNo { get; set; }
        public DateTime? RefundTime { get; set; }
        public DateTime? CancelTime { get; set; }

        /// <summary>原订单实付金额（元）——退款上限</summary>
        public decimal OrderPayAmount { get; set; }
        /// <summary>原订单已退金额（元）</summary>
        public decimal OrderRefundAmount { get; set; }
        /// <summary>原订单状态</summary>
        public OrderStatusEnum OrderStatus { get; set; }
        /// <summary>订单商品摘要，如「商品A×2；商品B×1」</summary>
        public string GoodsSummary { get; set; }
    }

    /// <summary>C 端申请售后</summary>
    public class AfterSaleApplyDto
    {
        /// <summary>订单 Id</summary>
        [Range(1L, long.MaxValue, ErrorMessage = "订单不能为空")]
        public long OrderId { get; set; }

        /// <summary>售后类型：1 仅退款 / 2 退货退款（未发货只能选仅退款）</summary>
        public AfterSaleTypeEnum Type { get; set; }

        /// <summary>申请退款金额（元）</summary>
        [Range(0.01, 9999999.99, ErrorMessage = "退款金额必须大于 0")]
        public decimal ApplyAmount { get; set; }

        [Required(ErrorMessage = "请选择售后原因")]
        [StringLength(100, ErrorMessage = "售后原因过长")]
        public string Reason { get; set; }

        [StringLength(500, ErrorMessage = "补充说明过长")]
        public string Describe { get; set; }
    }

    /// <summary>后台审核售后</summary>
    /// <remarks>
    /// 审核不调整金额：最终退多少在「登记渠道退款」那一步填（默认填申请额），
    /// 金额只在一处决定，避免审核填一半、登记又改一半而对不上账。
    /// </remarks>
    public class AfterSaleAuditDto
    {
        [Range(1L, long.MaxValue, ErrorMessage = "售后单不能为空")]
        public long Id { get; set; }

        /// <summary>是否通过</summary>
        public bool Approved { get; set; }

        /// <summary>审核备注；驳回时必填</summary>
        [StringLength(255, ErrorMessage = "审核备注过长")]
        public string AuditRemark { get; set; }
    }

    /// <summary>买家填写退货物流</summary>
    public class AfterSaleReturnDto
    {
        [Range(1L, long.MaxValue, ErrorMessage = "售后单不能为空")]
        public long Id { get; set; }

        [Required(ErrorMessage = "请选择退货物流公司")]
        [StringLength(50, ErrorMessage = "物流公司过长")]
        public string ReturnCompany { get; set; }

        [Required(ErrorMessage = "请填写退货物流单号")]
        [StringLength(64, ErrorMessage = "退货物流单号过长")]
        public string ReturnNo { get; set; }
    }

    /// <summary>后台登记渠道退款（售后收口动作）</summary>
    public class AfterSaleRefundDto
    {
        [Range(1L, long.MaxValue, ErrorMessage = "售后单不能为空")]
        public long Id { get; set; }

        /// <summary>实际退款金额（元）</summary>
        [Range(0.01, 9999999.99, ErrorMessage = "退款金额必须大于 0")]
        public decimal RefundAmount { get; set; }

        /// <summary>渠道退款单号（微信退款单号等），与渠道流水核对用</summary>
        [Required(ErrorMessage = "请填写渠道退款单号")]
        [StringLength(64, ErrorMessage = "渠道退款单号过长")]
        public string RefundChannelNo { get; set; }
    }
}
