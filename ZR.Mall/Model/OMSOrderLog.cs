using ZR.Mall.Enum;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 订单状态变更日志（谁、何时、从什么状态到什么状态、什么操作触发）
    /// </summary>
    [SugarTable("oms_order_log", "订单状态变更日志")]
    [Tenant("MallDb")]
    public class OMSOrderLog
    {
        /// <summary>
        /// 日志Id 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 订单Id 
        /// </summary>
        public long OrderId { get; set; }

        /// <summary>
        /// 订单号 
        /// </summary>
        [SugarColumn(Length = 64)]
        public string OrderNo { get; set; }

        /// <summary>
        /// 变更前状态（OrderStatusEnum 值）
        /// </summary>
        public int FromStatus { get; set; }

        /// <summary>
        /// 变更后状态（OrderStatusEnum 值）
        /// </summary>
        public int ToStatus { get; set; }

        /// <summary>
        /// 操作类型：PAY / CANCEL / EXPIRE / DELIVERY / REFUND
        /// </summary>
        [SugarColumn(Length = 32, IsNullable = true)]
        public string OperType { get; set; }

        /// <summary>
        /// 操作人（买家手机号 / 后台用户 / system(job)）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string Operator { get; set; }

        /// <summary>
        /// 备注 
        /// </summary>
        [SugarColumn(Length = 255, IsNullable = true)]
        public string Remark { get; set; }

        /// <summary>
        /// 创建时间 
        /// </summary>
        [SugarColumn(InsertServerTime = true)]
        public DateTime CreateTime { get; set; }
    }
}
