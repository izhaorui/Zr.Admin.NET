namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 支付流水查询对象
    /// </summary>
    public class OMSPaymentQueryDto : PagerInfo
    {
        /// <summary>
        /// 订单号（精确匹配）
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 支付类型：0->模拟；1->微信H5；2->支付宝（预留），null=全部
        /// </summary>
        public int? PayType { get; set; }

        /// <summary>
        /// 支付状态：0->预支付；1->已支付；2->已关闭；3->已退款，null=全部
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// 第三方支付流水号（模糊匹配）
        /// </summary>
        public string ChannelTradeNo { get; set; }

        public DateTime? BeginCreateTime { get; set; }
        public DateTime? EndCreateTime { get; set; }
    }

    /// <summary>
    /// 支付流水输入输出对象
    /// </summary>
    public class OMSPaymentDto
    {
        [ExcelColumn(Name = "流水Id")]
        public long Id { get; set; }

        [ExcelColumn(Name = "订单号")]
        public string OrderNo { get; set; }

        [ExcelColumn(Name = "订单Id")]
        public long? OrderId { get; set; }

        [ExcelColumn(Name = "支付方式")]
        public int PayType { get; set; }

        [ExcelColumn(Name = "渠道流水号")]
        public string ChannelTradeNo { get; set; }

        [ExcelColumn(Name = "金额")]
        public decimal Amount { get; set; }

        [ExcelColumn(Name = "支付状态")]
        public int Status { get; set; }

        [ExcelColumn(Name = "创建时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? CreateTime { get; set; }

        [ExcelColumn(Name = "支付时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? PayTime { get; set; }
    }
}
