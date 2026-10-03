using ZR.Mall.Common;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 售后单服务：买家申请 → 后台审核 →（退货：买家寄回 + 商家确认收货）→ 登记渠道退款 → 完成
    /// </summary>
    /// <remarks>
    /// 渠道退款由人工完成，服务只登记退款额与渠道退款单号；ApplyAmount 与 RefundAmount 可以不同，
    /// 对账以 RefundAmount 为准；状态推进一律用条件更新，避免并发重复推进。
    /// </remarks>
    [AppService(ServiceType = typeof(IOMSAfterSaleService))]
    public class OMSAfterSaleService : BaseService<OMSAfterSale>, IOMSAfterSaleService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IOMSOrderStateService _orderState;

        public OMSAfterSaleService(IOMSOrderStateService orderState)
        {
            _orderState = orderState;
        }

        #region 写操作

        /// <inheritdoc />
        public long Apply(long memberId, AfterSaleApplyDto dto)
        {
            // 枚举必须白名单校验：System.Text.Json 对数字枚举**不校验成员**，type=0 / 99 都会被原样绑定，
            // 结果是一条类型非法的售后单（后台列表显示"未知"，状态机按仅退款分支走）。
            if (dto.Type != AfterSaleTypeEnum.RefundOnly && dto.Type != AfterSaleTypeEnum.ReturnRefund)
            {
                throw new CustomException("售后类型不正确");
            }

            var order = Context.Queryable<OMSOrder>().First(x => x.Id == dto.OrderId && x.IsDelete == 0);
            // 归属只认会员身份，与订单接口同一条规则（OrderOwnership 是唯一实现）
            if (!OrderOwnership.IsOwnedBy(order, memberId))
            {
                throw new CustomException("订单不存在或无权限");
            }

            // 虚拟商品买家侧不可售后（形态判断只在本处与 C 端「申请售后」入口两处，页面里不要再判）
            if (order.OrderType == ProductTypeEnum.Virtual)
            {
                throw new CustomException("虚拟商品不支持售后");
            }

            // 售后时效：确认收货后 N 天内可申请（MallSettings:AfterSaleDays，默认 15，0 表示不限）。
            // 规则与阈值只在 MallAfterSalePolicy 一处，订单详情下发给 C 端的"是否超期"用的是同一个方法
            var afterSaleDays = MallAfterSalePolicy.ResolveDays();
            if (MallAfterSalePolicy.IsExpired(order, Context.GetDate(), afterSaleDays))
            {
                throw new CustomException(
                    $"已超过售后申请期限（确认收货后 {afterSaleDays} 天内可申请）。如有质量问题请联系客服处理");
            }

            // 只有已付款的订单才有款可退；待付款的正解是取消，不是售后
            if (order.OrderStatus != OrderStatusEnum.TobeShipped
                && order.OrderStatus != OrderStatusEnum.Shipped
                && order.OrderStatus != OrderStatusEnum.Completed)
            {
                throw new CustomException("该订单当前状态不支持申请售后");
            }

            // 没发货就没货可退，只能走仅退款
            if (dto.Type == AfterSaleTypeEnum.ReturnRefund && order.OrderStatus == OrderStatusEnum.TobeShipped)
            {
                throw new CustomException("订单尚未发货，请选择「仅退款」");
            }

            var refundable = order.PayAmount - order.RefundAmount;
            if (refundable <= 0m)
            {
                throw new CustomException("该订单已无可退金额");
            }
            if (dto.ApplyAmount > refundable)
            {
                throw new CustomException($"申请金额超过可退金额（可退 {refundable} 元）");
            }

            // 同一订单同时只允许一笔进行中的售后：多笔并行会让"可退余额"与库存回补互相踩
            var hasActive = Queryable().Any(x => x.OrderId == order.Id && x.IsDelete == 0
                && (x.Status == (int)AfterSaleStatusEnum.Pending
                    || x.Status == (int)AfterSaleStatusEnum.WaitingReturn
                    || x.Status == (int)AfterSaleStatusEnum.WaitingReceive
                    || x.Status == (int)AfterSaleStatusEnum.WaitingRefund));
            if (hasActive)
            {
                throw new CustomException("该订单已有进行中的售后申请，请等待处理完成");
            }

            var entity = new OMSAfterSale
            {
                // 售后单号复用订单号生成器：同为雪花ID，全局唯一、多实例安全、趋势递增
                AfterSaleNo = OrderNoGenerator.Generate(),
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                MemberId = order.MemberId,
                Type = (int)dto.Type,
                Status = (int)AfterSaleStatusEnum.Pending,
                ApplyAmount = dto.ApplyAmount,
                RefundAmount = 0m,
                Reason = dto.Reason?.Trim(),
                Describe = dto.Describe?.Trim(),
                ApplyTime = Context.GetDate(),
                IsDelete = 0,
            };

            var id = Context.Insertable(entity).ExecuteReturnBigIdentity();
            logger.Info($"售后申请已提交：AfterSaleNo={entity.AfterSaleNo} OrderNo={order.OrderNo} OrderId={order.Id} MemberId={order.MemberId} 类型={dto.Type} 申请金额={dto.ApplyAmount}元");
            return id;
        }

        /// <inheritdoc />
        public int Cancel(long id, long? memberId)
        {
            var entity = Queryable().First(x => x.Id == id && x.IsDelete == 0);
            if (memberId.HasValue && !OrderOwnership.IsOwnedBy(entity?.MemberId, memberId))
            {
                throw new CustomException("售后单不存在或无权限");
            }
            if (entity == null)
            {
                throw new CustomException("售后单不存在");
            }

            // 进入「待退款」后钱已在退，撤销无从生效；已完成更不该撤销
            if (entity.Status != (int)AfterSaleStatusEnum.Pending
                && entity.Status != (int)AfterSaleStatusEnum.WaitingReturn
                && entity.Status != (int)AfterSaleStatusEnum.WaitingReceive)
            {
                throw new CustomException("当前状态不允许撤销申请");
            }

            var affected = Context.Updateable<OMSAfterSale>()
                .SetColumns(it => new OMSAfterSale
                {
                    Status = (int)AfterSaleStatusEnum.Cancelled,
                    CancelTime = SqlFunc.GetDate()
                })
                .Where(x => x.Id == id && x.IsDelete == 0
                            && (x.Status == (int)AfterSaleStatusEnum.Pending
                                || x.Status == (int)AfterSaleStatusEnum.WaitingReturn
                                || x.Status == (int)AfterSaleStatusEnum.WaitingReceive))
                .ExecuteCommand();
            if (affected <= 0)
            {
                throw new CustomException("撤销失败：售后单状态已变更，请刷新后重试");
            }

            logger.Info($"售后已撤销：AfterSaleNo={entity.AfterSaleNo} OrderNo={entity.OrderNo} 原状态={(AfterSaleStatusEnum)entity.Status}");
            return 1;
        }

        /// <inheritdoc />
        public int Audit(AfterSaleAuditDto dto)
        {
            var entity = Queryable().First(x => x.Id == dto.Id && x.IsDelete == 0)
                ?? throw new CustomException("售后单不存在");
            if (entity.Status != (int)AfterSaleStatusEnum.Pending)
            {
                throw new CustomException("仅「待审核」的售后单可审核");
            }
            if (!dto.Approved && string.IsNullOrWhiteSpace(dto.AuditRemark))
            {
                throw new CustomException("驳回时必须填写原因");
            }

            // 通过后退款的单再次确认订单仍可退（审核期间订单可能已被别的流程退款）
            if (dto.Approved)
            {
                var order = Context.Queryable<OMSOrder>().First(x => x.Id == entity.OrderId && x.IsDelete == 0)
                    ?? throw new CustomException("原订单不存在");
                if (order.PayAmount - order.RefundAmount < entity.ApplyAmount)
                {
                    throw new CustomException($"订单可退余额已不足（可退 {order.PayAmount - order.RefundAmount} 元），请驳回或调整金额后重试");
                }
            }

            var targetStatus = dto.Approved
                ? (entity.Type == (int)AfterSaleTypeEnum.ReturnRefund
                    ? AfterSaleStatusEnum.WaitingReturn
                    : AfterSaleStatusEnum.WaitingRefund)
                : AfterSaleStatusEnum.Rejected;

            // 时间一律用 SqlFunc.GetDate()（数据库时间）：Context.GetDate() 是"取一次库时间"的方法调用，
            // 出现在 SetColumns 的表达式树里会让 SqlSugar 解析失败（实测 IndexOutOfRange）。
            // 其余值先落到局部变量再进表达式，避免表达式里出现 .Trim() / 静态属性这类不可翻译的成员访问。
            var auditBy = OperName;
            var auditRemark = dto.AuditRemark?.Trim();
            var affected = Context.Updateable<OMSAfterSale>()
                .SetColumns(it => new OMSAfterSale
                {
                    Status = (int)targetStatus,
                    AuditBy = auditBy,
                    AuditTime = SqlFunc.GetDate(),
                    AuditRemark = auditRemark
                })
                .Where(x => x.Id == dto.Id && x.Status == (int)AfterSaleStatusEnum.Pending && x.IsDelete == 0)
                .ExecuteCommand();
            if (affected <= 0)
            {
                throw new CustomException("审核失败：售后单状态已变更，请刷新后重试");
            }

            logger.Info($"售后审核完成：AfterSaleNo={entity.AfterSaleNo} OrderNo={entity.OrderNo} 结果={(dto.Approved ? "通过" : "驳回")} 目标状态={targetStatus} 审核人={OperName} 备注={dto.AuditRemark}");
            return 1;
        }

        /// <inheritdoc />
        public int FillReturnLogistics(long memberId, AfterSaleReturnDto dto)
        {
            var entity = Queryable().First(x => x.Id == dto.Id && x.IsDelete == 0);
            if (!OrderOwnership.IsOwnedBy(entity?.MemberId, memberId))
            {
                throw new CustomException("售后单不存在或无权限");
            }
            if (entity.Status != (int)AfterSaleStatusEnum.WaitingReturn)
            {
                throw new CustomException("当前状态不允许填写退货物流");
            }

            var returnCompany = dto.ReturnCompany.Trim();
            var returnNo = dto.ReturnNo.Trim();
            var affected = Context.Updateable<OMSAfterSale>()
                .SetColumns(it => new OMSAfterSale
                {
                    Status = (int)AfterSaleStatusEnum.WaitingReceive,
                    ReturnCompany = returnCompany,
                    ReturnNo = returnNo,
                    ReturnTime = SqlFunc.GetDate()
                })
                .Where(x => x.Id == dto.Id && x.Status == (int)AfterSaleStatusEnum.WaitingReturn && x.IsDelete == 0)
                .ExecuteCommand();
            if (affected <= 0)
            {
                throw new CustomException("提交失败：售后单状态已变更，请刷新后重试");
            }

            logger.Info($"售后退货物流已填写：AfterSaleNo={entity.AfterSaleNo} OrderNo={entity.OrderNo} 物流={dto.ReturnCompany} 单号={dto.ReturnNo}");
            return 1;
        }

        /// <inheritdoc />
        public int ConfirmReceive(long id, string remark)
        {
            var entity = Queryable().First(x => x.Id == id && x.IsDelete == 0)
                ?? throw new CustomException("售后单不存在");
            if (entity.Status != (int)AfterSaleStatusEnum.WaitingReceive)
            {
                throw new CustomException("仅「待商家收货」的售后单可确认收货");
            }

            var affected = Context.Updateable<OMSAfterSale>()
                .SetColumns(it => new OMSAfterSale
                {
                    Status = (int)AfterSaleStatusEnum.WaitingRefund,
                    ReceiveTime = SqlFunc.GetDate(),
                    ReceiveRemark = remark
                })
                .Where(x => x.Id == id && x.Status == (int)AfterSaleStatusEnum.WaitingReceive && x.IsDelete == 0)
                .ExecuteCommand();
            if (affected <= 0)
            {
                throw new CustomException("确认收货失败：售后单状态已变更，请刷新后重试");
            }

            logger.Info($"售后退货已确认收到：AfterSaleNo={entity.AfterSaleNo} OrderNo={entity.OrderNo} 操作人={OperName} 备注={remark}");
            return 1;
        }

        /// <inheritdoc />
        public int RegisterRefund(AfterSaleRefundDto dto)
        {
            var entity = Queryable().First(x => x.Id == dto.Id && x.IsDelete == 0)
                ?? throw new CustomException("售后单不存在");
            if (entity.Status != (int)AfterSaleStatusEnum.WaitingRefund)
            {
                throw new CustomException("仅「待退款」的售后单可登记退款");
            }

            var order = Context.Queryable<OMSOrder>().First(x => x.Id == entity.OrderId && x.IsDelete == 0)
                ?? throw new CustomException("原订单不存在");

            // 库存口径：只有货确实回到仓库才加回可售 ——
            // 退货退款：商家已确认收到（状态机保证），货已回仓 → 回补；
            // 仅退款：未发货时货还在仓 → 回补；已发货则货在买家手里且不退回 → 不回补。
            var restoreSellable = entity.Type == (int)AfterSaleTypeEnum.ReturnRefund
                || order.OrderStatus == OrderStatusEnum.TobeShipped;

            var tran = UseTran(() =>
            {
                var refundChannelNo = dto.RefundChannelNo.Trim();
                var affected = Context.Updateable<OMSAfterSale>()
                    .SetColumns(it => new OMSAfterSale
                    {
                        Status = (int)AfterSaleStatusEnum.Completed,
                        RefundAmount = dto.RefundAmount,
                        RefundChannelNo = refundChannelNo,
                        RefundTime = SqlFunc.GetDate()
                    })
                    .Where(x => x.Id == dto.Id && x.Status == (int)AfterSaleStatusEnum.WaitingRefund && x.IsDelete == 0)
                    .ExecuteCommand();
                if (affected <= 0)
                {
                    throw new CustomException("登记退款失败：售后单状态已变更，请刷新后重试");
                }

                // 订单侧落账（累计退款额 + 退款状态 + 按需回补库存）统一走订单状态内核，含超额护栏。
                // 失败会连带回滚上面的售后单状态 —— 两边的"退款已完成"必须同时成立。
                _orderState.ApplyAfterSaleRefund(order.Id, dto.RefundAmount, restoreSellable);
            });

            if (tran == null || !tran.IsSuccess)
            {
                logger.Error($"售后登记退款失败：AfterSaleNo={entity.AfterSaleNo} OrderNo={entity.OrderNo} 金额={dto.RefundAmount}元 错误={tran?.ErrorMessage}");
                throw new CustomException("登记退款失败" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }

            logger.Info($"售后已完成：AfterSaleNo={entity.AfterSaleNo} OrderNo={entity.OrderNo} OrderId={entity.OrderId} 类型={(AfterSaleTypeEnum)entity.Type} 退款={dto.RefundAmount}元 渠道单号={dto.RefundChannelNo} 回补可售={restoreSellable} 操作人={OperName}");
            return 1;
        }

        #endregion

        #region 查询

        /// <inheritdoc />
        public PagedInfo<AfterSaleDto> GetList(AfterSaleQueryDto parm)
        {
            parm ??= new AfterSaleQueryDto();
            var page = BuildQuery(parm).ToPage<OMSAfterSale, AfterSaleDto>(parm);
            FillOrderInfo(page.Result);
            return page;
        }

        /// <inheritdoc />
        public AfterSaleDto GetDetail(long id)
        {
            var entity = Queryable().First(x => x.Id == id && x.IsDelete == 0)
                ?? throw new CustomException("售后单不存在");
            var dto = entity.Adapt<AfterSaleDto>();
            FillOrderInfo(new List<AfterSaleDto> { dto });
            return dto;
        }

        /// <inheritdoc />
        public PagedInfo<AfterSaleDto> GetMemberList(long memberId, AfterSaleQueryDto parm)
        {
            parm ??= new AfterSaleQueryDto();
            // C 端一律按令牌里的会员过滤，不采信前端传的 MemberId（否则能翻别人的售后）
            parm.MemberId = memberId;
            var page = BuildQuery(parm).ToPage<OMSAfterSale, AfterSaleDto>(parm);
            FillOrderInfo(page.Result);
            return page;
        }

        /// <inheritdoc />
        public AfterSaleDto GetMemberDetail(long id, long memberId)
        {
            var entity = Queryable().First(x => x.Id == id && x.IsDelete == 0);
            if (!OrderOwnership.IsOwnedBy(entity?.MemberId, memberId))
            {
                throw new CustomException("售后单不存在或无权限");
            }
            return GetDetail(id);
        }

        private ISugarQueryable<OMSAfterSale> BuildQuery(AfterSaleQueryDto parm)
        {
            return Queryable()
                .WhereIF(!string.IsNullOrWhiteSpace(parm.AfterSaleNo), x => x.AfterSaleNo.Contains(parm.AfterSaleNo))
                .WhereIF(!string.IsNullOrWhiteSpace(parm.OrderNo), x => x.OrderNo.Contains(parm.OrderNo))
                .WhereIF(parm.Status.HasValue, x => x.Status == (int)parm.Status.Value)
                .WhereIF(parm.Type.HasValue, x => x.Type == (int)parm.Type.Value)
                .WhereIF(parm.MemberId.HasValue, x => x.MemberId == parm.MemberId.Value)
                .WhereIF(parm.BeginApplyTime.HasValue, x => x.ApplyTime >= parm.BeginApplyTime.Value)
                .WhereIF(parm.EndApplyTime.HasValue, x => x.ApplyTime <= parm.EndApplyTime.Value)
                .Where(x => x.IsDelete == 0)
                .OrderByDescending(x => x.Id);
        }

        /// <summary>
        /// 给售后单补上订单侧信息（实付 / 已退 / 订单状态 / 商品摘要）。
        /// </summary>
        /// <remarks>
        /// 两批查询补齐整页数据，不做逐行回查：售后列表一页 10~20 条，
        /// 逐条查订单与订单项就是 2×N 次往返。
        /// </remarks>
        private void FillOrderInfo(List<AfterSaleDto> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            var orderIds = rows.Select(x => x.OrderId).Distinct().ToList();
            var orders = Context.Queryable<OMSOrder>()
                .Where(x => orderIds.Contains(x.Id))
                .Select(x => new { x.Id, x.PayAmount, x.RefundAmount, x.OrderStatus })
                .ToList()
                .ToDictionary(x => x.Id);

            // 商品摘要：一次取回整页订单的订单项，在内存里按订单聚合
            var items = Context.Queryable<OMSOrderItem>()
                .Where(x => orderIds.Contains(x.OrderId))
                .Select(x => new { x.OrderId, x.ProductName, x.Quantity })
                .ToList()
                .GroupBy(x => x.OrderId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join("；", g.Select(i => $"{i.ProductName}×{i.Quantity}")));

            foreach (var row in rows)
            {
                if (orders.TryGetValue(row.OrderId, out var order))
                {
                    row.OrderPayAmount = order.PayAmount;
                    row.OrderRefundAmount = order.RefundAmount;
                    row.OrderStatus = (OrderStatusEnum)order.OrderStatus;
                }
                row.GoodsSummary = items.TryGetValue(row.OrderId, out var summary) ? summary : string.Empty;
            }
        }

        #endregion

        /// <summary>当前操作人：后台请求取登录用户名，定时任务/无上下文时记 system</summary>
        private static string OperName => string.IsNullOrWhiteSpace(App.UserName) ? "system" : App.UserName;
    }
}
