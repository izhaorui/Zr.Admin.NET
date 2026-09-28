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
        /// 游客/C端下单：校验库存、计算金额、写订单与订单项、乐观锁扣库存并累加销量。
        /// memberId 非空时把订单绑定到该会员（"我的订单"据此归属），为 null 时为游客匿名下单（行为不变）。
        /// </summary>
        OMSOrder CreateOrder(CreateOrderDto dto, long? memberId = null);

        /// <summary>
        /// 会员"我的订单"分页：会员本人下单(MemberId) + 同手机号的历史游客订单(GuestPhone) 合并
        /// </summary>
        PagedInfo<OMSOrderDto> GetMemberOrders(long memberId, string phone, MemberOrderQueryDto parm);

        /// <summary>
        /// 会员订单详情（校验归属，不属于该会员返回 null）
        /// </summary>
        OMSOrder GetMemberOrder(long memberId, string phone, long id);

        /// <summary>
        /// 会员取消自己的订单（校验归属后复用 CancelOrder：仅待付款/待发货可取消并回补库存）
        /// </summary>
        int CancelMemberOrder(long memberId, string phone, long id);
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
        OMSOrder PayOrderByOrderNo(string orderNo, ZR.Mall.Enum.PayTypeEnum payType = ZR.Mall.Enum.PayTypeEnum.Mock, string transactionId = null, string callbackRaw = null, long? callbackTotalFen = null);
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
