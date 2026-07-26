using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 订单管理service接口
    /// </summary>
    public interface IOMSOrderService : IBaseService<OMSOrder>
    {
        PagedInfo<OMSOrderDto> GetList(OMSOrderQueryDto parm);

        OMSOrder GetInfo(long Id);
        /// <summary>
        /// 游客/C端下单：校验库存、计算金额、写订单与订单项、乐观锁扣库存并累加销量
        /// </summary>
        OMSOrder CreateOrder(CreateOrderDto dto);
        int NotDelivereOrder();
        /// <summary>
        /// 订单各状态计数统计（用于列表顶部概览卡）
        /// </summary>
        Dictionary<int, int> GetOrderStatusStats();
        /// <summary>
        /// 取消订单（待付款/待发货可取消，回补库存）
        /// </summary>
        int CancelOrder(long id);
        /// <summary>
        /// 批量取消订单：仅处理待付款/待发货状态的订单，其余状态自动跳过；返回成功取消的条数
        /// </summary>
        int CancelOrders(List<long> ids);
        /// <summary>
        /// 模拟支付：待付款 → 待发货（订单号+手机号匹配，幂等）
        /// </summary>
        OMSOrder PayOrder(string orderNo, string phone);
        /// <summary>
        /// 按订单号完成支付（供游客支付与支付渠道异步回调复用）。幂等+条件更新。
        /// payType/transactionId 用于记录支付方式与第三方流水号（默认模拟支付）。
        /// callbackRaw 为支付渠道回调原文（微信/支付宝），写入支付流水表便于排查。
        /// </summary>
        OMSOrder PayOrderByOrderNo(string orderNo, ZR.Mall.Enum.PayTypeEnum payType = ZR.Mall.Enum.PayTypeEnum.Mock, string transactionId = null, string callbackRaw = null);
        /// <summary>
        /// 关闭超时未支付的待付款订单并回补库存（定时任务 Job_ClosePendingOrder 调用，幂等）
        /// </summary>
        int CloseExpiredPendingOrders(int expireMinutes = 30);
        int UpdateOMSOrder(int operType, OMSOrder parm);
        Task<int> OrderDelivery(OMSOrder model);
        int UpdateMerchantNote(OMSOrder model);
        PagedInfo<OMSOrderDto> ExportList(OMSOrderQueryDto parm);
        Task<List<DeliveryExpressDto>> ExportWaitDeliveryList(OMSOrderQueryDto parm);
        Task<dynamic> GetTotalSales(OMSOrderQueryDto dto);
        Task<dynamic> GetSaleTreandByDay(OMSOrderQueryDto dto);
        Task<dynamic> GetSaleTopProduct(OMSOrderQueryDto dto);
    }
}
