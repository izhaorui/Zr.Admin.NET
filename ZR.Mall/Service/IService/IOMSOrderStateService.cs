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
        /// 取消订单（待付款/待发货可取消，回补可售库存并冲减销量）。
        /// 条件更新：与支付、发货、超时关单并发时只有一方能命中，避免重复回补。
        /// 已支付订单此时并未退款（钱还在账上、渠道退款未发生），故只把退款状态置为"退款中(Refunding)"、
        /// 支付流水保持已支付；待运营在渠道侧退款完成后调用 <see cref="RefundOrder"/> 确认，才置为"已退款"。
        /// </summary>
        int CancelOrder(long id);

        /// <summary>
        /// 批量取消订单：仅处理待付款/待发货状态的订单，其余状态自动跳过；返回成功取消的条数
        /// </summary>
        int CancelOrders(List<long> ids);

        /// <summary>
        /// 确认收货（已发货 → 已完成，写 ConfirmTime）。幂等：已是已完成直接返回 1。
        /// 供会员在订单列表/详情手动确认；系统兜底走 <see cref="AutoConfirmReceipt"/>。
        /// </summary>
        int ConfirmReceipt(long id);

        /// <summary>
        /// 退款（运营动作，约定：调用前渠道退款已由人工在渠道侧完成）。
        /// ① 订单已取消且退款状态为"退款中"：仅确认退款完成（置已退款、补记已退金额并同步支付流水），不动订单状态与库存；
        /// ② 待付款：拒绝（没有款可退，正解是取消）；
        /// ③ 存在进行中的售后申请：拒绝（两个退钱入口各退一次就是重复退款）；
        /// ④ 其余未取消状态：关闭订单，未出库回补可售库存，已发货/已完成只冲减销量。
        /// 条件更新，重复退款不会二次回补。
        /// </summary>
        /// <remarks>
        /// 退款额按「实付 − 已退」的余量计算并累加进 <c>RefundAmount</c>，渠道侧也应按这个余量退。
        /// </remarks>
        int RefundOrder(OMSOrder model);

        /// <summary>
        /// 发货（待发货 → 已发货，释放锁定库存）
        /// </summary>
        /// <remarks>
        /// 同步返回（同本接口其余状态迁移）：「订单状态 + 释放锁定库存 + 写订单日志」在同一事务内，
        /// 短信通知在事务外。三者任一失败即整体回滚，避免出现"状态已发货但锁定库存永久锁死"。
        /// </remarks>
        /// <returns>&gt;0 成功；-1 已发货；-2 收货信息不完整；-3 订单不可发货；-4 物流信息不合法</returns>
        /// <param name="model">订单（发货相关的快递公司/单号取自本对象，其余字段应来自数据库）</param>
        /// <param name="preloadedItems">预加载的订单项，批量发货时一次查齐传入以避免 N+1；null 则本方法自行查询</param>
        /// <param name="notifyBuyer">是否发「已发货」短信，批量发货传 false</param>
        int OrderDelivery(OMSOrder model, List<OMSOrderItem> preloadedItems = null, bool notifyBuyer = true);

        /// <summary>
        /// 售后退款落账：累计订单的 RefundAmount、置退款状态、按情况回补库存（售后单流程调用）。
        /// 与 <see cref="RefundOrder"/> 的区别：售后发生在订单完成之后，**不把已完成订单改成已关闭**，
        /// 只在订单尚未发货时关闭；RefundAmount 累加且不得超过实付（超额护栏在 Where 里）。
        /// </summary>
        /// <param name="orderId">订单 Id</param>
        /// <param name="amount">本次实际退款金额（元）</param>
        /// <param name="restoreSellable">是否回补可售库存（仅当货确实回到仓库才为 true）</param>
        int ApplyAfterSaleRefund(long orderId, decimal amount, bool restoreSellable);

        /// <summary>
        /// 关闭超时未支付的待付款订单并回补库存（定时任务 Job_ClosePendingOrder 调用，幂等）
        /// </summary>
        int CloseExpiredPendingOrders(int expireMinutes = 30);

        /// <summary>
        /// 自动确认收货：已发货且发货时间超过 N 天的订单置为已完成（定时任务 Job_AutoConfirmReceipt 调用，幂等）。
        /// 与 <see cref="ConfirmReceipt"/> 的区别是"系统兜底"而非会员手动 ——
        /// 没有它订单会永远停在"已发货"，售后期限与商家结算都无法收口。
        /// </summary>
        /// <param name="autoConfirmDays">发货后自动确认的天数（默认 7）</param>
        int AutoConfirmReceipt(int autoConfirmDays = 7);
    }
}
