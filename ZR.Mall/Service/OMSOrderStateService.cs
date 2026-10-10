using ZR.Infrastructure.Helper;
using ZR.Mall.Common;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Member;
using ZR.ServiceCore.Services;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 订单状态内核实现：只做"状态迁移 + 库存副作用 + 必要通知"，不含查询/导出/统计。
    /// 被 C 端（FrontOrderService）、后台（OMSOrderService）、支付回调、定时任务共同依赖。
    /// </summary>
    [AppService(ServiceType = typeof(IOMSOrderStateService))]
    public class OMSOrderStateService : BaseService<OMSOrder>, IOMSOrderStateService
    {
        private readonly IOMSPaymentService _paymentService;
        private readonly IOMSOrderLogService _orderLogService;
        private readonly ISmsCodeLogService _smsCodeLogService;
        private readonly IMemberService _memberService;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paymentService"></param>
        /// <param name="orderLogService"></param>
        /// <param name="smsCodeLogService"></param>
        /// <param name="memberService"></param>
        public OMSOrderStateService(IOMSPaymentService paymentService, IOMSOrderLogService orderLogService,
            ISmsCodeLogService smsCodeLogService, IMemberService memberService)
        {
            _paymentService = paymentService;
            _orderLogService = orderLogService;
            _smsCodeLogService = smsCodeLogService;
            _memberService = memberService;
        }

        /// <summary>
        /// 按订单号完成支付（待付款 → 待发货），记录支付时间与支付方式/流水号。
        /// 幂等 + 条件更新（防并发重复支付 / 支付与取消竞态）。状态机不变。
        /// callbackTotalFen：支付渠道异步回调带回的金额（单位：分）。非 null 时与订单应付金额(PayAmount)做一致性校验，
        /// 防止伪造/篡改回调把订单标为已付（金额不符直接拒绝）。
        /// </summary>
        public OMSOrder PayOrderByOrderNo(string orderNo, Enum.PayTypeEnum payType = Enum.PayTypeEnum.Mock, string transactionId = null, string callbackRaw = null, long? callbackTotalFen = null)
        {
            var order = Queryable().First(x => x.OrderNo == orderNo && x.IsDelete == 0);
            if (order == null)
            {
                logger.Warn($"支付失败：订单不存在。OrderNo={orderNo} 支付方式={payType}");
                throw new CustomException("订单不存在");
            }
            // 回调金额一致性校验：微信/支付宝回调带回的是分，订单库为元，按四舍五入换算后比对
            if (callbackTotalFen.HasValue)
            {
                var expectedFen = Math.Round(order.PayAmount * 100, MidpointRounding.AwayFromZero);
                if (callbackTotalFen.Value != expectedFen)
                {
                    // 安全事件：金额不符可能是伪造/篡改回调，必须留痕（Error 级，进 error.log）
                    logger.Error($"支付回调金额不一致，已拒绝支付（疑似伪造回调）：OrderNo={orderNo} OrderId={order.Id} MemberId={order.MemberId} 订单应付={order.PayAmount}元({expectedFen}分) 回调={callbackTotalFen.Value}分 支付方式={payType}");
                    throw new CustomException("支付回调金额与订单金额不一致，支付失败");
                }
            }
            // 幂等：已支付直接返回（支付方式/流水号不覆盖，避免回调重复写入）
            if (order.OrderStatus == Enum.OrderStatusEnum.TobeShipped ||
                order.OrderStatus == Enum.OrderStatusEnum.Shipped ||
                order.OrderStatus == Enum.OrderStatusEnum.Completed)
            {
                // 渠道回调会重试，这里可能频繁命中
                logger.Debug($"支付回调幂等命中（订单已支付，忽略重复通知）：OrderNo={orderNo} OrderId={order.Id} 当前状态={order.OrderStatus} 支付方式={payType}");
                return order;
            }
            if (order.OrderStatus != Enum.OrderStatusEnum.None)
            {
                logger.Warn($"支付被拒绝：订单已取消或已关闭。OrderNo={orderNo} OrderId={order.Id} 当前状态={order.OrderStatus}");
                throw new CustomException("该订单已取消或已关闭，无法支付");
            }

            var dbDate = Context.GetDate();
            // 目标状态按形态分流：虚拟单付款即完成，不进「待发货」（其余状态机方法因此都不需要形态分支）
            var targetStatus = order.OrderType == Enum.ProductTypeEnum.Virtual
                ? Enum.OrderStatusEnum.Completed
                : Enum.OrderStatusEnum.TobeShipped;
            order.OrderStatus = targetStatus;
            order.PayTime = dbDate;
            order.PayType = (int)payType;
            order.TransactionId = transactionId;

            // 「订单状态置已付」与「写支付流水」必须在同一事务里。
            // 原实现分两步各自提交：若 UpsertPaid 抛错，订单已标已付而 oms_payment 缺这条流水；
            // 更糟的是渠道回调会重试，而重试命中上面的幂等分支（订单已是待发货/已发货/已完成）直接 return，
            // 于是这条缺失的流水**永远不会被补写** —— 对账时订单有金额、流水查无此单。
            // 收进事务后：UpsertPaid 失败整体回滚，订单状态回到"待付款"，回调重试即可走完整流程补上。
            var tran = UseTran(() =>
            {
                // 条件更新：仅待付款状态可支付成功（防并发重复支付/支付与取消竞态）
                var affected = Context.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder
                    {
                        OrderStatus = targetStatus,
                        PayTime = dbDate,
                        PayType = (int)payType,
                        TransactionId = transactionId
                    })
                    .Where(it => it.OrderNo == orderNo && it.OrderStatus == Enum.OrderStatusEnum.None && it.IsDelete == 0)
                    .ExecuteCommand();
                if (affected <= 0)
                {
                    // 条件更新未命中：校验与写入之间状态被并发改掉（另一请求已支付、或被取消/关单）
                    throw new CustomException("支付失败：订单状态已变更，请刷新后重试");
                }
                // 写入/更新支付流水（订单快照仅保留最近一次支付，完整生命周期以 oms_payment 为准）
                _paymentService.UpsertPaid(order, payType, transactionId, dbDate, callbackRaw);
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"支付落账事务失败：OrderNo={orderNo} OrderId={order.Id} 金额={order.PayAmount}元 支付方式={payType} 渠道流水号={transactionId} 错误={tran?.ErrorMessage}");
                throw new CustomException("支付失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }

            // 订单日志放事务外：AddLog 自身吞异常，放进去只会拉长事务（与取消/发货同一处理）
            // 虚拟单的目标状态是「已完成」，日志里点明原因，免得运营以为这一单漏了发货
            var payRemark = targetStatus == Enum.OrderStatusEnum.Completed
                ? "支付成功（虚拟商品，订单直接完成）"
                : "支付成功";
            _orderLogService.AddLog(order.Id, order.OrderNo, (int)Enum.OrderStatusEnum.None, (int)targetStatus, "PAY", order.GuestPhone, payRemark);
            // 不发通知：用户刚在页面上完成支付，已有即时反馈，再发一条只是烧短信费。
            // 将来若要"已支付"提醒，走站内信/订阅消息，不要走短信（见 NotifyBuyer 的发送策略）。
            logger.Info($"订单支付成功：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 金额={order.PayAmount}元 支付方式={payType} 渠道流水号={transactionId}");
            return order;
        }

        /// <summary>
        /// 取消订单（C 端会员/后台通用）：仅"待发货/待付款"可取消，回补库存
        /// </summary>
        public int CancelOrder(long id)
        {
            var db = Context;
            var dbDate = db.GetDate();
            var order = Queryable().Includes(x => x.Items).First(x => x.Id == id && x.IsDelete == 0);
            if (order == null)
            {
                logger.Warn($"取消订单失败：订单不存在。OrderId={id}");
                throw new CustomException("订单不存在");
            }
            if (order.OrderStatus != Enum.OrderStatusEnum.None && order.OrderStatus != Enum.OrderStatusEnum.TobeShipped)
            {
                logger.Warn($"取消订单被拒绝：状态不允许取消。OrderNo={order.OrderNo} OrderId={order.Id} 当前状态={order.OrderStatus}");
                throw new CustomException("仅待付款/待发货状态的订单可取消");
            }

            var fromStatus = order.OrderStatus;
            var paid = fromStatus == Enum.OrderStatusEnum.TobeShipped;
            var tran = UseTran(() =>
            {
                // 条件更新：只取消调用前读到的那个状态。支付/发货/超时关单抢先改掉时命中 0 行，不再回补。
                var affected = paid
                    ? Context.Updateable<OMSOrder>()
                        .SetColumns(it => new OMSOrder
                        {
                            OrderStatus = Enum.OrderStatusEnum.Cancel,
                            CancelTime = dbDate,
                            // 已支付订单此刻**还没退款**（钱在账上、渠道退款尚未发生），
                            // 因此置"退款中"而非"已退款"；待运营确认渠道退款完成时再推进（见 RefundOrder）。
                            RefundStatus = Enum.RefundStatusEnum.Refunding
                        })
                        .Where(x => x.Id == id && x.IsDelete == 0 && x.OrderStatus == fromStatus)
                        .ExecuteCommand()
                    : Context.Updateable<OMSOrder>()
                        .SetColumns(it => new OMSOrder
                        {
                            OrderStatus = Enum.OrderStatusEnum.Cancel,
                            CancelTime = dbDate
                        })
                        .Where(x => x.Id == id && x.IsDelete == 0 && x.OrderStatus == fromStatus)
                        .ExecuteCommand();
                if (affected <= 0)
                {
                    throw new CustomException("取消失败：订单状态已变更，请刷新后重试");
                }
                RestoreStock(order.Id, restoreSellable: true);
                if (!paid)
                {
                    // 未支付：把挂起的预支付流水关掉（Paid 流水不存在，无需处理）
                    _paymentService.CloseUnpaid(order.OrderNo);
                }
                // 已支付：**不动支付流水**（保持 Paid = 已收未退，与真实资金状态一致），
                // 待确认退款完成时由 RefundOrder 置为 Refunded。
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"取消订单事务失败：OrderNo={order.OrderNo} OrderId={order.Id} 原状态={(int)fromStatus} 错误={tran?.ErrorMessage}");
                throw new CustomException("取消订单失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            var cancelRemark = paid
                ? "取消已支付订单并回补库存（渠道退款需人工完成）"
                : "取消订单并回补库存";
            // 操作人：原实现传 null，出纠纷时订单日志里查不到是谁取消的。
            // 后台取消时取当前登录管理员；C 端会员自己取消时取不到登录用户，记 system
            // （该场景订单本身已带 MemberId，仍能追到人）。
            var cancelOper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
            _orderLogService.AddLog(order.Id, order.OrderNo, (int)fromStatus, (int)Enum.OrderStatusEnum.Cancel, "CANCEL", cancelOper, cancelRemark);
            // 不发短信：用户自己取消时页面已反馈；商家/系统取消的场景确实需要告知，
            // 但那属于站内信/订阅消息，不用短信（见 NotifyBuyer 的发送策略）。
            logger.Info($"订单已取消并回补库存：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 原状态={(int)fromStatus} 金额={order.PayAmount}元 已支付={paid}");
            return 1;
        }

        /// <summary>
        /// 确认收货（C 端会员）：已发货 → 已完成
        ///
        /// 上面的状态校验读的是调用前快照，校验与写入之间存在窗口，
        /// 并发确认会让双方都通过校验。放进 Where 后只有一方能命中。
        ///
        /// 不动库存：确认收货时锁定库存已在发货环节释放过（见 OrderDelivery），此处再动会重复扣减。
        /// </summary>
        public int ConfirmReceipt(long id)
        {
            var order = Queryable().First(x => x.Id == id && x.IsDelete == 0);
            if (order == null)
            {
                logger.Warn($"确认收货失败：订单不存在。OrderId={id}");
                throw new CustomException("订单不存在");
            }
            if (order.OrderStatus == Enum.OrderStatusEnum.Completed)
            {
                logger.Debug($"确认收货幂等命中（订单已完成）：OrderNo={order.OrderNo} OrderId={order.Id}");
                return 1;
            }
            if (order.OrderStatus != Enum.OrderStatusEnum.Shipped)
            {
                logger.Warn($"确认收货被拒绝：状态不允许。OrderNo={order.OrderNo} OrderId={order.Id} 当前状态={order.OrderStatus}");
                throw new CustomException("仅已发货状态的订单可确认收货");
            }

            var fromStatus = (int)order.OrderStatus;
            var rows = Update(x => x.Id == id && x.OrderStatus == Enum.OrderStatusEnum.Shipped, it => new OMSOrder
            {
                OrderStatus = Enum.OrderStatusEnum.Completed,
                // 签收即物流送达：DeliveryStatusEnum.Delivered 此前无任何写入方（死枚举），
                // 与 OrderStatus 双字段并存却不同步。补上写入点后两者才自洽：
                // OrderStatus 表达交易进度，DeliveryStatus 表达物流进度。
                DeliveryStatus = DeliveryStatusEnum.Delivered,
                ConfirmTime = SqlFunc.GetDate()
            });
            if (rows <= 0)
            {
                // 并发下被另一方抢先确认：按幂等处理，不当失败
                var alreadyDone = Context.Queryable<OMSOrder>().Any(x => x.Id == id && x.OrderStatus == Enum.OrderStatusEnum.Completed);
                logger.Debug($"确认收货条件更新未命中（并发处理）：OrderNo={order.OrderNo} OrderId={order.Id} 已完成为={alreadyDone}");
                return alreadyDone ? 1 : 0;
            }
            // 操作人：C 端会员确认收货时取不到登录管理员，记 system（订单本身带 MemberId，仍能追到人）。
            // 与取消/发货/退款的订单日志口径保持一致，否则同一张单的日志里只有确认收货这行没有操作人。
            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
            _orderLogService.AddLog(order.Id, order.OrderNo, fromStatus, (int)Enum.OrderStatusEnum.Completed, "CONFIRM_RECEIPT", oper, "用户确认收货");
            logger.Info($"订单确认收货完成：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 原状态={fromStatus}");
            return 1;
        }

        /// <summary>
        /// 自动确认收货（定时任务 Job_AutoConfirmReceipt 调用）：已发货且发货时间超过 N 天 → 已完成。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="ConfirmReceipt"/> 的区别：那个是会员手动点，这个由系统兜底 ——
        /// 没有它订单会永远停在"已发货"，售后期限与商家结算都无法收口。
        /// 幂等靠条件更新（仅"已发货"能命中）：重复执行只影响仍处于已发货且已超时的单。
        /// 不动库存：锁定库存在发货环节已释放（见 <see cref="OrderDelivery"/>），此处再动会重复扣减。
        /// </remarks>
        /// <param name="autoConfirmDays">发货后自动确认的天数（默认 7）</param>
        /// <returns>实际确认收货的订单数</returns>
        public int AutoConfirmReceipt(int autoConfirmDays = 7)
        {
            // 单轮处理上限：一轮跑太久会长时间占着事务；本任务按小时执行，超出的下一轮继续。
            const int batchSize = 500;

            var db = Context;
            var expireTime = db.GetDate().AddDays(-autoConfirmDays);
            int confirmed = 0;

            var tran = UseTran(() =>
            {
                // 候选：已发货且已超过阈值。ShipTime 为空的已发货单（异常/历史数据）用 CreateTime 兜底 ——
                // 否则它们永远等不到自动确认，正是本任务要消除的问题。
                var candidates = db.Queryable<OMSOrder>()
                    .Where(x => x.OrderStatus == Enum.OrderStatusEnum.Shipped
                                && x.IsDelete == 0
                                && ((x.ShipTime != null && x.ShipTime < expireTime)
                                    || (x.ShipTime == null && x.CreateTime < expireTime)))
                    .Select(x => new { x.Id, x.OrderNo })
                    .Take(batchSize)
                    .ToList();
                if (candidates.Count == 0)
                {
                    // 每轮都跑，无单可确认时不记 Info（避免刷屏）；留 Debug 便于确认任务在正常执行
                    logger.Debug($"自动确认收货：本轮无可确认订单。阈值={autoConfirmDays}天");
                    return;
                }

                var now = db.GetDate();
                foreach (var c in candidates)
                {
                    // 条件更新：仅当此刻仍是"已发货"才置为已完成。
                    // 并发下会员自己先确认过（或订单被退款关闭）会命中 0 行，跳过即可。
                    var affected = db.Updateable<OMSOrder>()
                        .SetColumns(it => new OMSOrder
                        {
                            OrderStatus = Enum.OrderStatusEnum.Completed,
                            // 同 ConfirmReceipt：自动确认同样代表"已签收"，两处口径必须一致，
                            // 否则会出现"手动确认的单显示已送达、自动确认的单永远停在已发货"
                            DeliveryStatus = DeliveryStatusEnum.Delivered,
                            ConfirmTime = now
                        })
                        .Where(x => x.Id == c.Id
                                    && x.OrderStatus == Enum.OrderStatusEnum.Shipped
                                    && x.IsDelete == 0)
                        .ExecuteCommand();
                    if (affected <= 0)
                    {
                        continue;
                    }

                    _orderLogService.AddLog(c.Id, c.OrderNo,
                        (int)Enum.OrderStatusEnum.Shipped, (int)Enum.OrderStatusEnum.Completed,
                        "AUTO_CONFIRM", "system", $"发货后 {autoConfirmDays} 天未操作，系统自动确认收货");
                    logger.Debug($"订单已自动确认收货：OrderNo={c.OrderNo} OrderId={c.Id}");
                    confirmed++;
                }
            });

            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"自动确认收货事务失败：阈值={autoConfirmDays}天 错误={tran?.ErrorMessage}");
                throw new CustomException("自动确认收货失败" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            if (confirmed > 0)
            {
                logger.Info($"自动确认收货完成：阈值={autoConfirmDays}天 确认数量={confirmed} 截止时间={expireTime}");
            }
            return confirmed;
        }

        /// <summary>
        /// 批量取消订单：一次性事务内遍历，仅处理待付款/待发货状态的订单，其余状态跳过；返回成功取消条数
        /// </summary>
        public int CancelOrders(List<long> ids)
        {
            if (ids == null || ids.Count == 0) return 0;

            var db = Context;
            var dbDate = db.GetDate();
            int count = 0;
            // 同 CancelOrder：批量取消是纯后台操作，必须记当前管理员，否则一次误操作无法追责
            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;

            var tran = UseTran(() =>
            {
                foreach (var id in ids)
                {
                    var order = Queryable().Includes(x => x.Items).First(x => x.Id == id && x.IsDelete == 0);
                    if (order == null) continue;
                    if (order.OrderStatus != Enum.OrderStatusEnum.None
                        && order.OrderStatus != Enum.OrderStatusEnum.TobeShipped)
                    {
                        continue; // 仅跳过不可取消的订单，不影响其余
                    }

                    var fromStatus = order.OrderStatus;
                    var paid = fromStatus == OrderStatusEnum.TobeShipped;
                    var affected = paid
                        ? db.Updateable<OMSOrder>()
                            .SetColumns(it => new OMSOrder
                            {
                                OrderStatus = OrderStatusEnum.Cancel,
                                CancelTime = dbDate,
                                // 同 CancelOrder：已支付但尚未退款，置"退款中"
                                RefundStatus = RefundStatusEnum.Refunding
                            })
                            .Where(x => x.Id == id && x.IsDelete == 0 && x.OrderStatus == fromStatus)
                            .ExecuteCommand()
                        : db.Updateable<OMSOrder>()
                            .SetColumns(it => new OMSOrder
                            {
                                OrderStatus = OrderStatusEnum.Cancel,
                                CancelTime = dbDate
                            })
                            .Where(x => x.Id == id && x.IsDelete == 0 && x.OrderStatus == fromStatus)
                            .ExecuteCommand();
                    if (affected <= 0)
                    {
                        // 校验与写入之间被支付/发货/关单改掉：跳过，不回补
                        continue;
                    }
                    RestoreStock(order.Id, restoreSellable: true);
                    if (!paid)
                    {
                        // 未支付：关掉挂起的预支付流水
                        _paymentService.CloseUnpaid(order.OrderNo);
                    }
                    // 已支付：不动支付流水（保持 Paid = 已收未退），待确认退款完成时由 RefundOrder 置为 Refunded
                    var batchRemark = paid
                        ? "批量取消已支付订单并回补库存（渠道退款需人工完成）"
                        : "批量取消并回补库存";
                    _orderLogService.AddLog(order.Id, order.OrderNo, (int)fromStatus, (int)OrderStatusEnum.Cancel, "CANCEL", oper, batchRemark);
                    // 不发短信：后台批量操作，逐单一条会在瞬间形成短信风暴（见 NotifyBuyer 的发送策略）
                    count++;
                }
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"批量取消订单事务失败：请求数={ids.Count} 已取消={count} 错误={tran?.ErrorMessage}");
                throw new CustomException("批量取消订单失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            // 批量操作只记汇总（逐单一条会刷屏，明细看订单操作日志表）
            logger.Info($"批量取消订单完成：请求数={ids.Count} 实际取消={count} 跳过={ids.Count - count} Ids={string.Join(",", ids.Take(20))}");
            return count;
        }

        /// <summary>
        /// 退款（运营动作，约定：调用前渠道退款已由人工在渠道侧完成）。
        /// ① 订单已取消且退款状态为"退款中"：仅确认退款完成（不动订单状态与库存，取消时已回补）；
        /// ② 待付款：**拒绝** —— 没有款可退，正解是「取消」（见 CancelOrder）；
        /// ③ 存在进行中的售后申请：**拒绝** —— 售后登记退款与本动作各退一次就是重复退款；
        /// ④ 待发货/已发货/已完成：关闭订单，待发货回补可售库存，已发货/已完成只冲减销量。
        /// 条件更新，避免重复回补。
        /// </summary>
        /// <remarks>
        /// 退款金额按「实付 - 已退」的余量计算并累加进 <c>RefundAmount</c>（与 <see cref="ApplyAfterSaleRefund"/> 同一口径），
        /// 渠道侧也应按这个余量退：直接把实付再退一遍就是重复退款。
        /// </remarks>
        public int RefundOrder(OMSOrder model)
        {
            var order = Queryable().Includes(x => x.Items).First(x => x.OrderNo == model.OrderNo && x.IsDelete == 0);
            if (order == null)
            {
                logger.Warn($"退款失败：订单不存在。OrderNo={model.OrderNo}");
                throw new CustomException("订单不存在");
            }

            // ① 取消后待确认渠道退款的单：只推进退款状态，避免"已取消但退款状态永远停在退款中"。
            if (order.OrderStatus == Enum.OrderStatusEnum.Cancel
                && order.RefundStatus == Enum.RefundStatusEnum.Refunding)
            {
                return ConfirmRefundCompleted(order);
            }

            if (order.OrderStatus == Enum.OrderStatusEnum.Cancel || order.OrderStatus == Enum.OrderStatusEnum.Closed)
            {
                logger.Warn($"退款被拒绝：订单状态不可退款。OrderNo={order.OrderNo} OrderId={order.Id} 当前状态={order.OrderStatus}");
                throw new CustomException("该订单状态不可退款");
            }

            // 未付款的单没有款可退，明确拒绝而不是"关单了事"。
            // 原实现对未付款单走的是「关闭订单 + 回补库存 + 退款状态置无退款」，看着像退款成功，
            // 实际一分钱都没退，运营看到的「已关闭 + 无退款」与按钮名完全对不上；
            // 更麻烦的是同一张待付款单走「取消」得到 Cancel(4)、走「退款」得到 Closed(5)，
            // 钱和库存处理一模一样、订单状态却分裂成两个值，后续对账要同时兼容两套口径。
            // 待付款单的正解是「取消」（同一行左侧就有那个按钮），由 CancelOrder 处理。
            if (order.OrderStatus == Enum.OrderStatusEnum.None)
            {
                logger.Warn($"退款被拒绝：订单未付款，无可退款项。OrderNo={order.OrderNo} OrderId={order.Id} 当前状态={order.OrderStatus}（应使用取消）");
                throw new CustomException("该订单未付款，无可退款项，请使用「取消」关闭订单");
            }

            var fromStatus = order.OrderStatus;
            // 走到这里必然已付款：待付款已被上面拒绝，已取消/已关闭已被更早的判断拦下，
            // 剩下的 TobeShipped/Shipped/Completed 三个状态都对应"渠道已收款"。
            // 故退款状态与支付流水都直接置已退款，无需再按"是否已付"分支。
            // 未出库（待发货）货还在仓库，退款加回可售；已发货/已完成货已出库，不能自动加库存。
            // 虚拟单没有"货已出库"这回事：退款后本就该能再卖，不回补则每退一单可售库存永久少一份
            var restoreSellable = fromStatus == Enum.OrderStatusEnum.TobeShipped
                || order.OrderType == Enum.ProductTypeEnum.Virtual;

            // 有进行中的售后时禁止整单退款：售后登记退款与本动作是两个独立退钱入口，
            // 各退一次就是重复退款（资损）；且本动作会把订单直接置 Closed，
            // 之后那笔售后单登记退款必然撞上"累计超过实付"护栏而永久卡在待退款。
            // 正确顺序：先把售后走完（或撤销），确认无进行中售后后再整单退款。
            var activeAfterSale = Context.Queryable<OMSAfterSale>()
                .Where(x => x.OrderId == order.Id && x.IsDelete == 0
                            && (x.Status == (int)AfterSaleStatusEnum.Pending
                                || x.Status == (int)AfterSaleStatusEnum.WaitingReturn
                                || x.Status == (int)AfterSaleStatusEnum.WaitingReceive
                                || x.Status == (int)AfterSaleStatusEnum.WaitingRefund))
                .OrderByDescending(x => x.Id)
                .First();
            if (activeAfterSale != null)
            {
                logger.Warn($"整单退款被拒绝：订单存在进行中的售后申请。OrderNo={order.OrderNo} OrderId={order.Id} AfterSaleNo={activeAfterSale.AfterSaleNo} 售后状态={(AfterSaleStatusEnum)activeAfterSale.Status}");
                throw new CustomException($"该订单有进行中的售后申请（{activeAfterSale.AfterSaleNo}），请先处理售后再整单退款");
            }

            // 退"实付 - 已退"的余量，而不是直接退实付：订单可能已通过售后退过一部分。
            // 原实现既不写 RefundAmount 也按实付记账，导致 C 端/后台显示"已退 0 元"却标着已退款，
            // 售后的可退余额（PayAmount - RefundAmount）也随之虚高。
            var remainAmount = order.PayAmount - order.RefundAmount;
            if (remainAmount <= 0m)
            {
                logger.Warn($"整单退款被拒绝：订单已无可退金额。OrderNo={order.OrderNo} OrderId={order.Id} 实付={order.PayAmount}元 已退={order.RefundAmount}元");
                throw new CustomException("该订单已无可退金额");
            }

            var tran = UseTran(() =>
            {
                var affected = Context.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder
                    {
                        OrderStatus = Enum.OrderStatusEnum.Closed,
                        RefundStatus = Enum.RefundStatusEnum.Refunded,
                        // 与 ApplyAfterSaleRefund 同一口径：已退金额必须累加并留护栏，
                        // 否则两条退款链路各记各的，对账时以 RefundAmount 为准的那个是错的
                        RefundAmount = it.RefundAmount + remainAmount
                    })
                    .Where(x => x.OrderNo == model.OrderNo && x.IsDelete == 0 && x.OrderStatus == fromStatus
                                && x.RefundAmount + remainAmount <= x.PayAmount)
                    .ExecuteCommand();
                if (affected <= 0)
                {
                    // 两种可能：订单状态被并发变更，或"已退 + 本次"超过实付被护栏拦下
                    throw new CustomException("退款失败：订单状态或已退金额已变更，请刷新后重试");
                }
                RestoreStock(order.Id, restoreSellable);
                _paymentService.MarkRefunded(order.OrderNo);
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"退款事务失败：OrderNo={order.OrderNo} OrderId={order.Id} 本次退款={remainAmount}元 累计={order.PayAmount}元 原状态={(int)fromStatus} 错误={tran?.ErrorMessage}");
                throw new CustomException("退款失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            // 退款是后台人工动作：渠道退款由人工在支付渠道完成后，再来这里做"标记 + 回补库存"，
            // 因此必须留下操作人，便于与渠道退款流水核对。
            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
            var refundStockRemark = restoreSellable
                ? "并回补库存"
                : "（已出库，未自动加回可售库存）";
            var refundRemark = $"整单退款 {remainAmount} 元（退款后累计已退 {order.PayAmount} 元）{refundStockRemark}，渠道退款由人工完成";
            _orderLogService.AddLog(order.Id, order.OrderNo, (int)fromStatus, (int)Enum.OrderStatusEnum.Closed, "REFUND", oper, refundRemark);
            var refundNotice = restoreSellable
                ? $"您的订单({order.OrderNo})已退款 {remainAmount} 元，款项将原路退回，库存已回补。"
                : $"您的订单({order.OrderNo})已退款 {remainAmount} 元，款项将原路退回。";
            NotifyBuyer(order, "订单已退款", refundNotice);
            logger.Info($"订单退款完成：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 本次退款={remainAmount}元 累计已退={order.PayAmount}元 实付={order.PayAmount}元 原状态={(int)fromStatus} 回补可售={restoreSellable} 操作人={oper}");
            return 1;
        }

        /// <summary>
        /// 确认退款完成：把"已取消 + 退款中(Refunding)"的订单推进为"已退款(Refunded)"，并同步支付流水为已退款。
        /// 不动订单状态与库存（取消时已回补），仅用于运营在渠道侧退款完成后做标记 ——
        /// 这是"取消已支付订单"之后唯一的收口动作，否则退款状态会永远停在"退款中"。
        /// </summary>
        private int ConfirmRefundCompleted(OMSOrder order)
        {
            // 确认渠道退款完成 = 钱确实退了，RefundAmount 必须同步补记（取消动作本身不写该字段）。
            // 正常路径下这里 RefundAmount 仍为 0（已支付订单的取消是"待退款"状态，不写金额），
            // 但订单可能先经售后退过一部分，故按余量补记而不是写死 PayAmount —— 与 RefundOrder 同一口径。
            var remainAmount = order.PayAmount - order.RefundAmount;

            // 「订单退款状态/已退金额」与「支付流水置已退款」同事务：原实现分两步提交，
            // MarkRefunded 失败时订单已标已退款而 oms_payment 仍停在 Paid，
            // 对账会看到"订单退了、流水没退"，且没有重试入口（订单已不在退款中状态）。
            // 与 RefundOrder / ApplyAfterSaleRefund 的处理口径一致。
            var tran = UseTran(() =>
            {
                var affected = Context.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder
                    {
                        RefundStatus = Enum.RefundStatusEnum.Refunded,
                        RefundAmount = it.RefundAmount + remainAmount
                    })
                    .Where(x => x.Id == order.Id && x.IsDelete == 0 && x.RefundStatus == Enum.RefundStatusEnum.Refunding
                                && x.RefundAmount + remainAmount <= x.PayAmount)
                    .ExecuteCommand();
                if (affected <= 0)
                {
                    // 并发下已被另一次确认改掉：报错让运营刷新（幂等重试即可）
                    throw new CustomException("确认退款失败：退款状态已变更，请刷新后重试");
                }
                _paymentService.MarkRefunded(order.OrderNo);
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"确认退款事务失败：OrderNo={order.OrderNo} OrderId={order.Id} 本次={remainAmount}元 错误={tran?.ErrorMessage}");
                throw new CustomException("确认退款失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }

            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
            _orderLogService.AddLog(order.Id, order.OrderNo, (int)order.OrderStatus, (int)order.OrderStatus,
                "REFUND_CONFIRM", oper, $"确认渠道退款完成，退款 {remainAmount} 元（累计已退 {order.PayAmount} 元）");
            NotifyBuyer(order, "退款已完成", $"您的订单({order.OrderNo})退款 {remainAmount} 元已完成，款项将原路退回。");
            logger.Info($"确认退款完成：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 本次={remainAmount}元 累计已退={order.PayAmount}元 实付={order.PayAmount}元 操作人={oper}");
            return 1;
        }

        /// <summary>
        /// 售后退款落账：把售后单的退款写进订单（累计 RefundAmount、置退款状态、按情况回补库存）。
        /// </summary>
        /// <remarks>
        /// 与 RefundOrder 的差异：不把订单置为已关闭（仅未发货时关闭），只累加退款额并置退款状态；
        /// 库存按调用方传入的 restoreSellable 回补，累计退款额在 Where 里设护栏不超过实付。
        /// </remarks>
        /// <param name="orderId">订单 Id</param>
        /// <param name="amount">本次实际退款金额（元）</param>
        /// <param name="restoreSellable">是否回补可售库存</param>
        /// <returns>落账成功返回 1</returns>
        public int ApplyAfterSaleRefund(long orderId, decimal amount, bool restoreSellable)
        {
            if (amount <= 0m)
            {
                throw new CustomException("退款金额必须大于 0");
            }

            var order = Queryable().First(x => x.Id == orderId && x.IsDelete == 0);
            if (order == null)
            {
                logger.Warn($"售后退款落账失败：订单不存在。OrderId={orderId}");
                throw new CustomException("订单不存在");
            }
            if (order.OrderStatus == Enum.OrderStatusEnum.Cancel || order.OrderStatus == Enum.OrderStatusEnum.Closed)
            {
                logger.Warn($"售后退款落账被拒绝：订单状态不可退款。OrderNo={order.OrderNo} OrderId={order.Id} 当前状态={order.OrderStatus}");
                throw new CustomException("该订单状态不可退款");
            }

            var fromStatus = order.OrderStatus;
            // 未发货的订单必须关闭，否则商家仍可能发货；已发货/已完成保持原状态（见 remarks）
            var targetStatus = fromStatus == Enum.OrderStatusEnum.TobeShipped
                ? Enum.OrderStatusEnum.Closed
                : fromStatus;

            var tran = UseTran(() =>
            {
                var affected = Context.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder
                    {
                        OrderStatus = targetStatus,
                        RefundAmount = it.RefundAmount + amount,
                        RefundStatus = Enum.RefundStatusEnum.Refunded
                    })
                    .Where(x => x.Id == orderId
                                && x.IsDelete == 0
                                && x.OrderStatus == fromStatus
                                && x.RefundAmount + amount <= x.PayAmount)
                    .ExecuteCommand();
                if (affected <= 0)
                {
                    // 两种可能：状态被并发变更，或"累加后超过实付"被护栏拦下 —— 必须区分，提示完全不同
                    var current = Context.Queryable<OMSOrder>().First(x => x.Id == orderId);
                    if (current != null && current.RefundAmount + amount > current.PayAmount)
                    {
                        logger.Warn($"售后退款落账被拒绝：退款额将超过实付。OrderNo={current.OrderNo} 已退={current.RefundAmount}元 本次={amount}元 实付={current.PayAmount}元");
                        throw new CustomException("退款金额超过订单实付金额，已拒绝（请核对是否重复退款）");
                    }
                    throw new CustomException("退款落账失败：订单状态已变更，请刷新后重试");
                }

                RestoreStock(orderId, restoreSellable);
                _paymentService.MarkRefunded(order.OrderNo);
            });

            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"售后退款落账事务失败：OrderNo={order.OrderNo} OrderId={order.Id} 本次金额={amount}元 错误={tran?.ErrorMessage}");
                throw new CustomException("退款落账失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }

            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
            var totalRefunded = order.RefundAmount + amount;
            _orderLogService.AddLog(order.Id, order.OrderNo, (int)fromStatus, (int)targetStatus,
                "AFTER_SALE_REFUND", oper,
                $"售后退款 {amount} 元（累计 {totalRefunded}/{order.PayAmount}），渠道退款由人工完成，回补可售={restoreSellable}");
            NotifyBuyer(order, "退款已完成", $"您的订单({order.OrderNo})退款 {amount} 元已完成，款项将原路退回。");
            logger.Info($"售后退款落账完成：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 本次={amount}元 累计={totalRefunded}元 实付={order.PayAmount}元 原状态={(int)fromStatus} 目标状态={(int)targetStatus} 回补可售={restoreSellable} 操作人={oper}");
            return 1;
        }

        /// <summary>
        /// 按 SKU 合计回冲下单副作用（事务内调用）。
        /// restoreSellable=true：可售加回、锁定释放、销量冲减（取消、超时关单、未发货退款）。
        /// restoreSellable=false：只冲减销量（已发货/已完成退款，货已出库，锁定已在发货时释放）。
        /// 同一 SKU 多行先加总再更新，避免按行更新与发货释放口径不一致。
        /// </summary>
        private void RestoreStock(long orderId, bool restoreSellable)
        {
            var items = Context.Queryable<OMSOrderItem>().Where(i => i.OrderId == orderId).ToList();
            foreach (var group in items.GroupBy(i => i.SkuId))
            {
                var qty = group.Sum(i => i.Quantity);
                var skuId = group.Key;
                if (restoreSellable)
                {
                    Context.Updateable<Skus>()
                        .SetColumns(s => new Skus
                        {
                            Stock = s.Stock + qty,
                            LockStock = SqlFunc.IIF(s.LockStock - qty < 0, 0, s.LockStock - qty),
                            SalesVolume = SqlFunc.IIF(s.SalesVolume - qty < 0, 0, s.SalesVolume - qty)
                        })
                        .Where(s => s.SkuId == skuId)
                        .ExecuteCommand();
                }
                else
                {
                    Context.Updateable<Skus>()
                        .SetColumns(s => new Skus
                        {
                            SalesVolume = SqlFunc.IIF(s.SalesVolume - qty < 0, 0, s.SalesVolume - qty)
                        })
                        .Where(s => s.SkuId == skuId)
                        .ExecuteCommand();
                }
            }

            // 回写商品总销量/总库存（退款/取消会冲减销量、回补库存），与本方法同一事务连接
            ProductTotalsRefresher.Refresh(Context, items.Select(i => i.ProductId));
        }

        /// <summary>
        /// 发货
        /// </summary>
        /// <param name="model"></param>
        /// <param name="preloadedItems">
        /// 预加载的订单项。批量发货时由调用方一次查齐传入，避免每单再查一次（N+1）。
        /// 为 null 时本方法自行查询；传入则<strong>必须</strong>是 model.Id 对应的订单项。
        /// </param>
        /// <param name="notifyBuyer">
        /// 是否给买家发「已发货」短信。批量发货传 false：100 单就是 100 次同步网络往返 + 100 条短信费用，
        /// 而批量发货本就不逐单通知买家（行业惯例，见 NotifyBuyer 的发送策略说明）。
        /// </param>
        /// <returns> >0 成功；-1 已发货；-2 收货信息不完整；-3 订单不可发货；-4 物流信息不合法</returns>
        public int OrderDelivery(OMSOrder model, List<OMSOrderItem> preloadedItems = null, bool notifyBuyer = true)
        {
            // ── 校验段：必须留在 UseTran 之前 ──
            // UseTran 会把事务内异常收敛成 tran.ErrorMessage，若把校验放进事务，用户只会看到
            // 一句"发货失败，请重试"，而真正的原因（已发货/地址不全/状态不对）就丢了。
            if (model.DeliveryStatus != DeliveryStatusEnum.NotDelivered)
            {
                logger.Warn($"发货被拒绝：该订单已发货。OrderNo={model.OrderNo} OrderId={model.Id} 发货状态={model.DeliveryStatus}");
                return -1;//已发货
            }
            // 地址完整性：原先只判 == null，但下单一单能传进一个"字段全为 null 的空对象"，
            // 校验会通过 → 发货成功而买家根本收不到货。逐项判空才拦得住（口径见 DeliveryValidator）。
            var address = model.AddressSnapshot;
            if (!DeliveryValidator.IsAddressComplete(address))
            {
                logger.Warn($"发货被拒绝：收货信息不完整。OrderNo={model.OrderNo} OrderId={model.Id} 收件人={address?.UserName} 手机号={MaskUtil.MaskPhone(address?.Phone)} 地区={address?.Province} 详细地址={(string.IsNullOrWhiteSpace(address?.DetailAddress) ? "(空)" : "(有)")}");
                return -2; // 收货信息不完整
            }
            // 单号与快递公司：这里只兜"空/超长"，格式规则统一在 DeliveryValidator（Controller 入口已先校验过，
            // 这里再兜一道是因为本方法也可能被批量导入等其它路径直接调用）。
            if (string.IsNullOrWhiteSpace(model.DeliveryNo) || model.DeliveryNo.Length > DeliveryValidator.MaxNoLength)
            {
                logger.Warn($"发货被拒绝：物流单号为空或超长。OrderNo={model.OrderNo} OrderId={model.Id} 快递={model.DeliveryCompany} 单号长度={model.DeliveryNo?.Length ?? 0}");
                return -4;
            }
            if (string.IsNullOrWhiteSpace(model.DeliveryCompany) || model.DeliveryCompany.Length > DeliveryValidator.MaxCompanyLength)
            {
                logger.Warn($"发货被拒绝：物流公司为空或超长。OrderNo={model.OrderNo} OrderId={model.Id} 快递长度={model.DeliveryCompany?.Length ?? 0}");
                return -4;
            }
            // 仅"待发货"订单可发货，防止把待付款/已取消/已关闭/已发货单误改为已发货
            if (model.OrderStatus != OrderStatusEnum.TobeShipped)
            {
                logger.Warn($"发货被拒绝：订单非待发货状态。OrderNo={model.OrderNo} OrderId={model.Id} 订单状态={model.OrderStatus}");
                return -3; // 非待发货状态，不可发货
            }

            // 订单项只读、且发货不改订单项，故放在事务外查：缩短事务持锁时间。
            // 批量发货由调用方一次查齐后传入（preloadedItems），避免每单重复查一次。
            var items = preloadedItems
                ?? Context.Queryable<OMSOrderItem>().Where(i => i.OrderId == model.Id).ToList();

            // 操作人：发货是后台人工动作，必须留痕。原实现传 null，出纠纷时订单日志里查不到是谁发的货
            // （sys_oper_log 里有，但 oms_order_log 里没有）。取不到登录用户时记 system，与退款一致。
            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;

            // ── 写阶段（事务内）：订单状态 + 释放锁定库存 + 订单日志，三者必须原子 ──
            // 原实现先提交订单状态、再在 try 里释放库存，catch 只记日志不回滚：
            // 一旦库存释放失败，订单显示已发货但 LockStock 永久锁死（且再点发货会被 -1 拒绝、无法补救）。
            // 库存锁死不可逆，故这里必须与状态同生共死。
            string failReason = null;
            var tran = UseTran(() =>
            {
                // 条件更新：除订单号外再校验"待发货"（参照 PayOrderByOrderNo 的防并发写法）。
                // 上面的 model.OrderStatus 校验读的是调用前快照，校验与写入之间存在窗口，
                // 并发重复发货会双双通过校验、重复释放锁定库存；把状态放进 Where 后只有一方能命中。
                var affected = Context.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder()
                    {
                        DeliveryCompany = model.DeliveryCompany,
                        DeliveryNo = model.DeliveryNo,
                        OrderStatus = OrderStatusEnum.Shipped, // 已发货
                        DeliveryStatus = DeliveryStatusEnum.Delivering, // 已发货
                        ShipTime = SqlFunc.GetDate()
                    })
                    .Where(w => w.OrderNo == model.OrderNo && w.OrderStatus == OrderStatusEnum.TobeShipped)
                    .ExecuteCommand();

                if (affected <= 0)
                {
                    // 并发下已被另一请求发货、或订单被取消/关闭：此时既不能释放锁定库存，也不应写发货日志。
                    // 用标志位而非抛异常传出去 —— 抛异常会被 UseTran 收敛成事务失败，
                    // 用户看到的将是"发货失败，请重试"而不是"状态已变更，请刷新"。
                    failReason = "发货条件更新未命中（并发发货或订单已被取消/关闭）";
                    return;
                }

                // 按 SKU 合计释放锁定库存。同一 SKU 拆成多行时不能按「数量相同的行」做 IN：
                // IN 会把重复 SkuId 收成一行，只减一次数量，锁定库存少释放。
                foreach (var group in items.GroupBy(i => i.SkuId))
                {
                    var skuId = group.Key;
                    var qty = group.Sum(i => i.Quantity);
                    Context.Updateable<Skus>()
                        .SetColumns(s => new Skus { LockStock = SqlFunc.IIF(s.LockStock - qty < 0, 0, s.LockStock - qty) })
                        .Where(s => s.SkuId == skuId)
                        .ExecuteCommand();
                }

                // 走 Service 而非直接 Insert：AddLog 会自动填 CreateTime，且与取消/退款等
                // 其他状态日志保持同一写法（详见 IOMSOrderLogService.AddLog）。
                _orderLogService.AddLog(model.Id, model.OrderNo,
                    (int)OrderStatusEnum.TobeShipped, (int)OrderStatusEnum.Shipped,
                    "DELIVERY", oper, $"{model.DeliveryCompany} {model.DeliveryNo}");
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"发货事务失败：OrderNo={model.OrderNo} OrderId={model.Id} 快递={model.DeliveryCompany} 运单号={model.DeliveryNo} 原状态={(int)OrderStatusEnum.TobeShipped} 错误={tran?.ErrorMessage}");
                throw new CustomException("发货失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            if (failReason != null)
            {
                logger.Warn($"发货条件更新未命中：OrderNo={model.OrderNo} OrderId={model.Id} 原因={failReason}");
                return -3;
            }

            // 通知买家挪到事务外：短信网关抖动不该回滚已完成的发货（NotifyBuyer 自身已吞异常，
            // 放在事务内只会拉长事务、放大锁窗口，没有任何收益）。
            // 批量发货传 notifyBuyer=false 跳过：逐单发短信是 100 次同步网络往返 + 100 条费用，
            // 而批量发货场景买家本就靠订单状态查看进度（与 NotifyBuyer 只发发货/退款两类的策略一致）。
            if (notifyBuyer)
            {
                NotifyBuyer(model, "订单已发货", $"您的订单({model.OrderNo})已发货，{model.DeliveryCompany} 运单号 {model.DeliveryNo}，请留意查收。");
            }
            logger.Info($"订单发货成功：OrderNo={model.OrderNo} OrderId={model.Id} MemberId={model.MemberId} 快递={model.DeliveryCompany} 运单号={model.DeliveryNo} 订单项数={items.Count} 操作人={oper}");
            return 1;
        }

        /// <summary>
        /// 关闭超时未支付的待付款订单并回补库存（定时任务 Job_ClosePendingOrder 调用）。
        /// 用条件更新（仅待付款且超时）保证幂等：重复执行只会影响仍处于待付款且超时的单，
        /// 已取消的单不再命中，且回补仅针对"本次实际变更"的订单，不会重复回补库存。
        /// </summary>
        /// <param name="expireMinutes">未支付超时分钟数（默认30）</param>
        /// <returns>实际关闭的订单数</returns>
        public int CloseExpiredPendingOrders(int expireMinutes = 30)
        {
            var db = Context;
            var expireTime = db.GetDate().AddMinutes(-expireMinutes);
            int closed = 0;
            var tran = UseTran(() =>
            {
                // 1) 先取本轮候选订单（仅"待付款且已超时"），只取 Id/订单号，避免整单实体入内存。
                //    关单与回补都以这份候选列表为准 —— 原实现用「CancelTime 在最近 10 秒内」反推
                //    "哪些是本次关的"，会把同一时段被用户/后台取消的老订单也算进来，
                //    对同一笔取消重复回补库存（CancelOrder 已回补过一次）。
                var candidates = db.Queryable<OMSOrder>()
                    .Where(x => x.OrderStatus == Enum.OrderStatusEnum.None
                                && x.IsDelete == 0
                                && x.CreateTime < expireTime)
                    .Select(x => new { x.Id, x.OrderNo })
                    .ToList();
                if (candidates.Count == 0)
                {
                    // 定时任务每轮都会跑，无单可关时不记 Info（避免刷屏）；保留 Debug 便于确认任务在正常执行
                    logger.Debug($"超时订单清理：本轮无待付款超时订单。超时阈值={expireMinutes}分钟");
                    return;
                }

                var now = db.GetDate();
                // 2) 逐单条件关单：仅当该单此刻仍是"待付款且超时"才置为取消。
                //    条件更新天然防并发（并发支付/取消的那一单会命中 0 行被跳过）；
                //    且只对"本次确实关掉的这一单"回补库存，不再依赖时间窗口去猜，杜绝重复回补。
                foreach (var c in candidates)
                {
                    var affected = db.Updateable<OMSOrder>()
                        .SetColumns(it => new OMSOrder
                        {
                            OrderStatus = Enum.OrderStatusEnum.Cancel,
                            CancelTime = now
                        })
                        .Where(x => x.Id == c.Id
                                    && x.OrderStatus == Enum.OrderStatusEnum.None
                                    && x.IsDelete == 0
                                    && x.CreateTime < expireTime)
                        .ExecuteCommand();
                    if (affected <= 0)
                    {
                        // 校验与写入之间被并发改掉（已支付/已取消）：跳过，不回补库存
                        continue;
                    }

                    RestoreStock(c.Id, restoreSellable: true);
                    // 收口预支付流水：用户在超时前点过"去支付"会留下 Prepay 记录，
                    // 不关掉的话订单已取消、流水却永远停在"预支付"（与取消/退款路径口径不一致）。
                    _paymentService.CloseUnpaid(c.OrderNo);
                    _orderLogService.AddLog(c.Id, c.OrderNo, (int)Enum.OrderStatusEnum.None, (int)Enum.OrderStatusEnum.Cancel, "EXPIRE", "system", "超时未支付自动关闭并回补库存");
                    logger.Debug($"超时订单已关闭并回补库存：OrderNo={c.OrderNo} OrderId={c.Id}");
                    closed++;
                }
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"超时订单清理事务失败：超时阈值={expireMinutes}分钟 错误={tran?.ErrorMessage}");
                throw new CustomException("超时订单清理失败" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            if (closed > 0)
            {
                logger.Info($"超时未支付订单自动关闭完成：超时阈值={expireMinutes}分钟 关闭数量={closed} 截止时间={expireTime}");
            }
            return closed;
        }

        /// <summary>
        /// 通知买家订单变化（**对外可达的短信通道**）。
        ///
        /// 发送策略：只发两类"必须主动触达"的事件 ——
        /// - 发货：状态由商家推进、用户在等货，运单号要靠它送达；
        /// - 退款：钱相关，必须让用户知道。
        /// 支付成功、取消订单**不在这里发**：支付成功用户刚在页面上操作过、已有即时反馈；
        /// 取消订单（含商家/系统触发）更适合站内信或订阅消息 —— 用短信只是烧钱，
        /// 批量取消更糟：逐单一条会在瞬间形成短信风暴。
        ///
        /// 接收号码：**只发会员本人手机号**。
        /// 收货手机号是联系方式（可能是家人/朋友的），未经归属证明，绝不能当发送目标 ——
        /// 否则就等于开放了一个"给任意号码发短信"的接口。
        /// 异常仅记录，不影响主流程。
        /// </summary>
        private void NotifyBuyer(OMSOrder order, string title, string content)
        {
            if (order == null) return;
            try
            {
                var phone = GetNotifyPhone(order);
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    _smsCodeLogService.SendSmsNotice(phone, $"[{title}] {content}", 6);
                    logger.Info($"订单通知短信已发送：OrderNo={order.OrderNo} 场景={title} 接收号码={MaskUtil.MaskPhone(phone)}");
                }
                else
                {
                    // 取不到会员手机号时宁可不发（见 GetNotifyPhone），但必须留痕，
                    // 否则"用户说没收到短信"时无从判断是"没发"还是"发了没到"
                    logger.Warn($"订单通知未发送：取不到接收号码（会员手机号缺失或会员已删除）。OrderNo={order.OrderNo} 场景={title} MemberId={order.MemberId}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"订单通知短信发送失败（不影响订单状态本身）：OrderNo={order.OrderNo} 场景={title}");
            }
        }

        /// <summary>
        /// 订单通知的接收手机号：**只取会员本人手机号**。
        /// 会员手机号取不到（未绑定会员、会员被删或数据异常）时返回 null —— 宁可不发，
        /// 也不把通知发到未经归属证明的收货号上。
        /// </summary>
        private string GetNotifyPhone(OMSOrder order)
        {
            if (!order.MemberId.HasValue || order.MemberId.Value <= 0)
            {
                return null;
            }
            try
            {
                var member = _memberService.GetById(order.MemberId.Value);
                return member != null && member.IsDelete == 0 ? member.Phone : null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"读取会员手机号失败（订单通知将跳过）：MemberId={order.MemberId} OrderNo={order.OrderNo}");
                return null;
            }
        }
    }
}

