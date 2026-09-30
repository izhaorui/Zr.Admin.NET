using ZR.Mall.Model;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 订单状态内核：所有会改动订单状态或库存的原子操作，供 C 端、后台、支付回调、定时任务共用。
    ///
    /// 为什么单独存在：这些操作（支付成功 / 取消+回补 / 退款 / 发货 / 超时关单）是"订单业务真相"所在，
    /// 且被多个入口复用（C 端支付与微信回调、会员取消与后台取消、后台发货与定时任务）。
    /// 集中一处可保证状态机与库存副作用只有一份实现；
    /// 而查询/导出/统计（IOMSOrderService）与 C 端交易编排（IFrontOrderService）各司其职。
    /// </summary>
    public interface IOMSOrderStateService
    {
        /// <summary>
        /// 按订单号完成支付（待付款 → 待发货）。幂等 + 条件更新（防并发重复支付 / 支付与取消竞态）。
        /// 供 C 端直接支付（PayOrder 会员鉴权后）与支付渠道异步回调（微信/支付宝 notify）调用。
        /// callbackTotalFen：回调带回的金额（分），非 null 时与订单应付金额做一致性校验，防伪造回调。
        /// </summary>
        OMSOrder PayOrderByOrderNo(string orderNo, ZR.Mall.Enum.PayTypeEnum payType = ZR.Mall.Enum.PayTypeEnum.Mock, string transactionId = null, string callbackRaw = null, long? callbackTotalFen = null);

        /// <summary>
        /// 取消订单（待付款/待发货可取消，回补库存）。供会员取消与后台取消共用。
        /// </summary>
        int CancelOrder(long id);

        /// <summary>
        /// 批量取消订单：仅处理待付款/待发货状态的订单，其余状态自动跳过；返回成功取消的条数
        /// </summary>
        int CancelOrders(List<long> ids);

        /// <summary>
        /// 确认收货（已发货 → 已完成，写 ConfirmTime）。幂等：已是已完成直接返回 1。
        /// 供会员在订单列表/详情手动确认；系统自动确认收货的定时任务将来也走这里，保证状态机只有一份。
        /// </summary>
        int ConfirmReceipt(long id);

        /// <summary>
        /// 退款：关闭订单 + 回补库存（简易版无支付，退款即退回库存）
        /// </summary>
        int RefundOrder(OMSOrder model);

        /// <summary>
        /// 发货（待发货 → 已发货，释放锁定库存）
        /// </summary>
        Task<int> OrderDelivery(OMSOrder model);

        /// <summary>
        /// 关闭超时未支付的待付款订单并回补库存（定时任务 Job_ClosePendingOrder 调用，幂等）
        /// </summary>
        int CloseExpiredPendingOrders(int expireMinutes = 30);
    }
}
