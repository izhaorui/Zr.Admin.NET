
using ZR.Mall.Enum;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 订单管理
    /// </summary>
    [SugarTable("oms_order", "订单表")]
    [SugarIndex("index_orderNo", nameof(OrderNo), OrderByType.Asc, true)]
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
        /// 用户ID 
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// 赠送用户ID
        /// </summary>
        public long? ToUserId { get; set; }

        /// <summary>
        /// 总金额 
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// 付款金额 
        /// </summary>
        public decimal PayAmount { get; set; }

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