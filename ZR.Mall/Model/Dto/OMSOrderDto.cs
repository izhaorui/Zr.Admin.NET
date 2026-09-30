
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

        [ExcelColumn(Name = "总金额")]
        public decimal TotalAmount { get; set; }

        [ExcelColumn(Name = "付款金额")]
        public decimal PayAmount { get; set; }

        [ExcelColumn(Name = "支付方式")]
        public int? PayType { get; set; }

        [ExcelColumn(Name = "支付流水号")]
        public string TransactionId { get; set; }

        [ExcelColumn(Name = "订单状态", Ignore = true)]
        public int? OrderStatus { get; set; }

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