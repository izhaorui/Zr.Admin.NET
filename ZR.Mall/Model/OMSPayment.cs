using ZR.Mall.Enum;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 支付流水表（每笔支付/预支付一条记录，支持多渠道、多次支付与对账）。
    /// 订单表 oms_order 上的 PayType/TransactionId 作为"最近一次支付"快照供列表快速展示，
    /// 完整生命周期以本表为准。
    /// </summary>
    [SugarTable("oms_payment", "支付流水表")]
    [Tenant("MallDb")]
    public class OMSPayment
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 订单号（与 oms_order.OrderNo 关联）
        /// </summary>
        [SugarColumn(Length = 32, IsNullable = true)]
        public string OrderNo { get; set; }

        /// <summary>
        /// 订单Id（冗余，便于 join）
        /// </summary>
        public long? OrderId { get; set; }

        /// <summary>
        /// 支付类型：0->模拟；1->微信H5；2->支付宝（预留）
        /// </summary>
        public int PayType { get; set; }

        /// <summary>
        /// 第三方支付流水号（微信 transaction_id / 支付宝 trade_no）；模拟支付为空
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string ChannelTradeNo { get; set; }

        /// <summary>
        /// 支付金额
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 支付状态：0->预支付；1->已支付；2->已关闭；3->已退款
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 预支付凭证（微信 H5 的 mweb_url 等）
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string PrepayData { get; set; }

        /// <summary>
        /// 支付渠道回调原文（JSON），便于排查/对账
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string CallbackRaw { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>
        /// 支付成功时间
        /// </summary>
        public DateTime? PayTime { get; set; }

        /// <summary>
        /// 是否删除(软删除) 1.已删除 0.未删除
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDelete { get; set; }
    }
}
