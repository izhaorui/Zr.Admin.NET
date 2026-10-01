using ZR.Infrastructure.Helper;
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
            // 条件更新：仅待付款状态可支付成功（防并发重复支付/支付与取消竞态）
            var affected = Context.Updateable<OMSOrder>()
                .SetColumns(it => new OMSOrder
                {
                    OrderStatus = Enum.OrderStatusEnum.TobeShipped,
                    PayTime = dbDate,
                    PayType = (int)payType,
                    TransactionId = transactionId
                })
                .Where(it => it.OrderNo == orderNo && it.OrderStatus == Enum.OrderStatusEnum.None && it.IsDelete == 0)
                .ExecuteCommand();
            if (affected <= 0)
            {
                // 条件更新未命中：校验与写入之间状态被并发改掉（另一请求已支付、或被取消/关单）
                logger.Warn($"支付失败：订单状态已变更（并发重复支付或取消竞态）。OrderNo={orderNo} OrderId={order.Id} 支付方式={payType}");
                throw new CustomException("支付失败：订单状态已变更，请刷新后重试");
            }
            order.OrderStatus = Enum.OrderStatusEnum.TobeShipped;
            order.PayTime = dbDate;
            order.PayType = (int)payType;
            order.TransactionId = transactionId;
            // 写入/更新支付流水（订单快照仅保留最近一次支付，完整生命周期以 oms_payment 为准）
            _paymentService.UpsertPaid(order, payType, transactionId, dbDate, callbackRaw);
            _orderLogService.AddLog(order.Id, order.OrderNo, (int)Enum.OrderStatusEnum.None, (int)Enum.OrderStatusEnum.TobeShipped, "PAY", order.GuestPhone, "支付成功");
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

            var fromStatus = (int)order.OrderStatus;
            var tran = UseTran(() =>
            {
                Update(x => x.Id == id, it => new OMSOrder
                {
                    OrderStatus = Enum.OrderStatusEnum.Cancel,
                    CancelTime = dbDate
                });
                RestoreStock(order.Id);
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"取消订单事务失败：OrderNo={order.OrderNo} OrderId={order.Id} 原状态={fromStatus} 错误={tran?.ErrorMessage}");
                throw new CustomException("取消订单失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            _orderLogService.AddLog(order.Id, order.OrderNo, fromStatus, (int)Enum.OrderStatusEnum.Cancel, "CANCEL", null, "取消订单并回补库存");
            // 不发短信：用户自己取消时页面已反馈；商家/系统取消的场景确实需要告知，
            // 但那属于站内信/订阅消息，不用短信（见 NotifyBuyer 的发送策略）。
            logger.Info($"订单已取消并回补库存：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 原状态={fromStatus} 金额={order.PayAmount}元");
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
                ConfirmTime = SqlFunc.GetDate()
            });
            if (rows <= 0)
            {
                // 并发下被另一方抢先确认：按幂等处理，不当失败
                var alreadyDone = Context.Queryable<OMSOrder>().Any(x => x.Id == id && x.OrderStatus == Enum.OrderStatusEnum.Completed);
                logger.Debug($"确认收货条件更新未命中（并发处理）：OrderNo={order.OrderNo} OrderId={order.Id} 已完成为={alreadyDone}");
                return alreadyDone ? 1 : 0;
            }
            _orderLogService.AddLog(order.Id, order.OrderNo, fromStatus, (int)Enum.OrderStatusEnum.Completed, "CONFIRM_RECEIPT", null, "用户确认收货");
            logger.Info($"订单确认收货完成：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 原状态={fromStatus}");
            return 1;
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

                    var fromStatus = (int)order.OrderStatus;
                    Update(x => x.Id == id, it => new OMSOrder
                    {
                        OrderStatus = Enum.OrderStatusEnum.Cancel,
                        CancelTime = dbDate
                    });
                    RestoreStock(order.Id);
                    _orderLogService.AddLog(order.Id, order.OrderNo, fromStatus, (int)OrderStatusEnum.Cancel, "CANCEL", null, "批量取消并回补库存");
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
        /// 退款：关闭订单 + 回补库存（简易版无支付，退款即退回库存）
        /// </summary>
        public int RefundOrder(OMSOrder model)
        {
            var db = Context;
            var dbDate = db.GetDate();
            var order = Queryable().Includes(x => x.Items).First(x => x.OrderNo == model.OrderNo && x.IsDelete == 0);
            if (order == null)
            {
                logger.Warn($"退款失败：订单不存在。OrderNo={model.OrderNo}");
                throw new CustomException("订单不存在");
            }
            if (order.OrderStatus == Enum.OrderStatusEnum.Cancel || order.OrderStatus == Enum.OrderStatusEnum.Closed)
            {
                logger.Warn($"退款被拒绝：订单状态不可退款。OrderNo={order.OrderNo} OrderId={order.Id} 当前状态={order.OrderStatus}");
                throw new CustomException("该订单状态不可退款");
            }

            var fromStatus = (int)order.OrderStatus;
            var tran = UseTran(() =>
            {
                Update(x => x.OrderNo == model.OrderNo, it => new OMSOrder
                {
                    OrderStatus = Enum.OrderStatusEnum.Closed,
                    RefundStatus = Enum.RefundStatusEnum.Refunded
                });
                RestoreStock(order.Id);
            });
            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"退款事务失败：OrderNo={order.OrderNo} OrderId={order.Id} 退款金额={order.PayAmount}元 原状态={fromStatus} 错误={tran?.ErrorMessage}");
                throw new CustomException("退款失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            // 退款是后台人工动作：渠道退款由人工在支付渠道完成后，再点这里做"标记 + 回补库存"，
            // 因此必须留下操作人，便于与渠道退款流水核对。
            var oper = string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
            _orderLogService.AddLog(order.Id, order.OrderNo, fromStatus, (int)Enum.OrderStatusEnum.Closed, "REFUND", oper, "订单退款并回补库存（渠道退款由人工完成）");
            NotifyBuyer(order, "订单已退款", $"您的订单({order.OrderNo})已退款，款项将原路退回，库存已回补。");
            logger.Info($"订单退款完成并回补库存：OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 退款金额={order.PayAmount}元 原状态={fromStatus} 操作人={oper}");
            return 1;
        }

        /// <summary>
        /// 按订单项回补 SKU 库存（事务内调用，单条 UPDATE 原子加回）
        /// </summary>
        private void RestoreStock(long orderId)
        {
            var items = Context.Queryable<OMSOrderItem>().Where(i => i.OrderId == orderId).ToList();
            foreach (var it in items)
            {
                // 回补可售库存，并释放对应的锁定库存（IIF 防负：已发货出库释放过的保持 0）
                Context.Updateable<Skus>()
                    .SetColumns(s => new Skus
                    {
                        Stock = s.Stock + it.Quantity,
                        LockStock = SqlFunc.IIF(s.LockStock - it.Quantity < 0, 0, s.LockStock - it.Quantity)
                    })
                    .Where(s => s.SkuId == it.SkuId)
                    .ExecuteCommand();
            }
        }

        /// <summary>
        /// 发货
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<int> OrderDelivery(OMSOrder model)
        {
            if (model.DeliveryStatus != DeliveryStatusEnum.NotDelivered)
            {
                logger.Warn($"发货被拒绝：该订单已发货。OrderNo={model.OrderNo} OrderId={model.Id} 发货状态={model.DeliveryStatus}");
                return -1;//已发货
            }
            if (model.AddressSnapshot == null)
            {
                logger.Warn($"发货被拒绝：收货地址为空。OrderNo={model.OrderNo} OrderId={model.Id}");
                return -2; // 地址信息不能为空
            }
            // 仅"待发货"订单可发货，防止把待付款/已取消/已关闭/已发货单误改为已发货
            if (model.OrderStatus != OrderStatusEnum.TobeShipped)
            {
                logger.Warn($"发货被拒绝：订单非待发货状态。OrderNo={model.OrderNo} OrderId={model.Id} 订单状态={model.OrderStatus}");
                return -3; // 非待发货状态，不可发货
            }
            // 条件更新：除订单号外再校验"待发货"（参照 PayOrderByOrderNo 的防并发写法）。
            // 上面的 model.OrderStatus 校验读的是调用前的快照，校验与写入之间存在窗口，
            // 并发重复发货会双双通过校验、重复释放锁定库存；把状态放进 Where 后只有一方能命中。
            var result = await UpdateAsync(w => w.OrderNo == model.OrderNo && w.OrderStatus == OrderStatusEnum.TobeShipped, it => new OMSOrder()
            {
                DeliveryCompany = model.DeliveryCompany,
                DeliveryNo = model.DeliveryNo,
                OrderStatus = OrderStatusEnum.Shipped, // 已发货
                DeliveryStatus = DeliveryStatusEnum.Delivering, // 已发货
                ShipTime = SqlFunc.GetDate()
            });
            // 发货成功 → 释放锁定库存 + 写日志 + 通知买家（短信）
            if (result > 0)
            {
                try
                {
                    var items = await Context.Queryable<OMSOrderItem>()
                        .Where(i => i.OrderId == model.Id).ToListAsync();

                    // 释放锁定库存：按扣减数量分组，每组一条 UPDATE ... WHERE SkuId IN (...)，
                    // 替代原先"每个订单项一条 UPDATE"（买 5 种商品就发 5 条 SQL）。典型订单数量一致，只占 1 次往返。
                    // 扣减仍写在 SQL 端（LockStock - qty，下限 0）而不是先读后算，避免并发发货丢更新。
                    foreach (var group in items.GroupBy(i => i.Quantity))
                    {
                        var skuIds = group.Select(i => i.SkuId).ToList();
                        var qty = group.Key;
                        await Context.Updateable<Skus>()
                            .SetColumns(s => new Skus { LockStock = SqlFunc.IIF(s.LockStock - qty < 0, 0, s.LockStock - qty) })
                            .Where(s => skuIds.Contains(s.SkuId))
                            .ExecuteCommandAsync();
                    }

                    await _orderLogService.AddLogAsync(model.Id, model.OrderNo, (int)OrderStatusEnum.TobeShipped, (int)OrderStatusEnum.Shipped, "DELIVERY", null, $"{model.DeliveryCompany} {model.DeliveryNo}");
                    NotifyBuyer(model, "订单已发货", $"您的订单({model.OrderNo})已发货，{model.DeliveryCompany} 运单号 {model.DeliveryNo}，请留意查收。");
                    logger.Info($"订单发货成功：OrderNo={model.OrderNo} OrderId={model.Id} MemberId={model.MemberId} 快递={model.DeliveryCompany} 运单号={model.DeliveryNo} 订单项数={items.Count}");
                }
                catch (Exception ex)
                {
                    // 库存/通知异常仅记录，不影响发货结果（订单状态已置为已发货）
                    logger.Error(ex, $"发货成功但后续处理失败（订单状态已更新为已发货，锁定库存/通知可能未完成）：OrderNo={model.OrderNo} OrderId={model.Id} 快递={model.DeliveryCompany} 运单号={model.DeliveryNo}");
                }
            }
            else if (result == 0)
            {
                // 条件更新未命中：状态在上面的校验之后被并发变更（另一请求已发货、或订单被取消/关闭）。
                // 此时不能再释放锁定库存，也不应写第二条发货日志，按"不可发货"返回。
                logger.Warn($"发货条件更新未命中（并发发货或被取消/关闭）：OrderNo={model.OrderNo} OrderId={model.Id}");
                return -3;
            }
            return result;
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

                    RestoreStock(c.Id);
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

