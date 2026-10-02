using Infrastructure.Extensions;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 后台订单管理 Service（运营侧）：查询、概览、修改/备注、导出、销售统计。
    ///
    /// 职责边界（与本次拆分前相比）：状态迁移（发货/取消/退款/支付）已移至 <see cref="IOMSOrderStateService"/>；
    /// C 端交易（下单/支付/我的订单/会员取消）已移至 <see cref="FrontOrderService"/>。
    /// 本类只保留"运营视角"的只读查询与编排，不再直接改订单状态或库存。
    /// </summary>
    [AppService(ServiceType = typeof(IOMSOrderService))]
    public class OMSOrderService : BaseService<OMSOrder>, IOMSOrderService
    {
        private readonly IOMSOrderStateService _orderStateService;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public OMSOrderService(IOMSOrderStateService orderStateService)
        {
            _orderStateService = orderStateService;
        }

        /// <summary>
        /// 查询订单管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<OMSOrderDto> GetList(OMSOrderQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Includes(x => x.Items)
                .Where(predicate.ToExpression())
                .ToPage<OMSOrder, OMSOrderDto>(parm);

            return response;
        }

        /// <summary>
        /// 订单概览统计（列表顶部概览卡）：一次 GROUP BY 同时得出「各状态计数」与「待发货未发货数」。
        ///
        /// 合并原因：原先列表接口要跑 3 条 SQL（分页列表 + NotDelivereOrder + GetOrderStatusStats），
        /// 后两者统计的是同一张表、同一批数据，只是维度不同 —— 按 (状态, 发货状态) 分组后，
        /// 各状态计数是它的上卷，"待发货未发货"正好是其中一个分组，一次查询即可全部得出。
        /// 仅统计未删除订单(IsDelete=0)。
        /// </summary>
        public OrderOverviewDto GetOrderOverview()
        {
            var rows = Queryable()
                .Where(f => f.IsDelete == 0)
                .GroupBy(f => new { f.OrderStatus, f.DeliveryStatus })
                .Select(f => new
                {
                    f.OrderStatus,
                    f.DeliveryStatus,
                    Count = SqlFunc.AggregateCount(f.Id)
                })
                .ToList();

            var overview = new OrderOverviewDto();
            foreach (var row in rows)
            {
                var status = (int)row.OrderStatus;
                overview.StatusStats[status] = overview.StatusStats.TryGetValue(status, out var exists)
                    ? exists + row.Count
                    : row.Count;

                if (row.OrderStatus == Enum.OrderStatusEnum.TobeShipped
                    && row.DeliveryStatus == Enum.DeliveryStatusEnum.NotDelivered)
                {
                    overview.NotDelivered += row.Count;
                }
            }
            return overview;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public OMSOrder GetInfo(long Id)
        {
            var response = Queryable()
                .Includes(x => x.Items)
                .Where(x => x.Id == Id)
                .First();

            return response;
        }

        /// <summary>
        /// 修改订单管理（按 <see cref="OrderUpdateOperType"/> 分发）
        /// </summary>
        /// <param name="operType"><see cref="OrderUpdateOperType"/> 的值</param>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateOMSOrder(int operType, OMSOrder model)
        {
            // 除退款（内部按订单号查）外，其余分支都按 OrderNo 定位。OrderNo 为空时
            // Where 会退化成"命中所有 OrderNo 为空的订单"，必须挡住。
            if (string.IsNullOrWhiteSpace(model?.OrderNo))
            {
                throw new CustomException("订单号不能为空");
            }

            switch ((OrderUpdateOperType)operType)
            {
                //修改商家备注
                case OrderUpdateOperType.MerchantNote:
                    return UpdateMerchantNote(model);
                //修改地址：已发货及之后地址已交给物流，不再改快照
                case OrderUpdateOperType.Address:
                    var addressRows = Update(w => w.OrderNo == model.OrderNo && w.IsDelete == 0
                        && (w.OrderStatus == Enum.OrderStatusEnum.None || w.OrderStatus == Enum.OrderStatusEnum.TobeShipped), it => new OMSOrder()
                    {
                        AddressSnapshot = model.AddressSnapshot,
                    });
                    if (addressRows <= 0)
                    {
                        throw new CustomException("仅待付款或待发货的订单可修改收货地址");
                    }
                    return addressRows;
                //订单退款：关闭订单 + 回补库存（简易版无支付，退款即退回库存）—— 状态迁移动交状态内核
                case OrderUpdateOperType.Refund:
                    return _orderStateService.RefundOrder(model);
                //通用更新：原实现是 Update(model, true)（按主键整体更新非空字段），
                //意味着请求方传 OrderStatus / PayAmount / TotalAmount 就能直接改订单状态与金额 —— 属于越权改账。
                //改为字段白名单：只允许改"非交易字段"，交易字段一律由状态内核（OMSOrderStateService）维护。
                case OrderUpdateOperType.General:
                    return Update(w => w.OrderNo == model.OrderNo, it => new OMSOrder()
                    {
                        MerchantNote = model.MerchantNote,
                        OrderNote = model.OrderNote,
                    });
                default:
                    // 原实现对未识别值静默走 Update(model, true)，误传操作码会改动整单字段；改为明确报错
                    throw new CustomException($"不支持的订单操作类型：{operType}");
            }
        }

        /// <summary>
        /// 软删除订单。待付款/待发货先走取消（条件更新 + 回补库存），再标记删除，
        /// 避免逻辑删除后超时关单看不到这张单、锁定库存无法释放。
        ///
        /// ⚠️ 语义：删除"已支付未发货"订单会走取消流程 —— 等价于「取消 + 待退款」
        /// （退款状态置为 Refunding，渠道退款仍由人工完成）。若只想清理脏数据而不触发退款，
        /// 请先与运营确认或改走状态变更。
        ///
        /// 并发：单笔取消时若订单状态已被支付/发货/关单改掉，按跳过处理（该单不会被删除），
        /// 其余继续。原实现让异常直接冒泡，会留下"前面已删、后面未处理"的半成品且无回滚。
        /// </summary>
        public int DeleteOrders(long[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return 0;
            }

            var requestIds = ids.Distinct().ToList();
            var count = 0;
            var skipped = 0;
            foreach (var id in requestIds)
            {
                var order = Queryable().First(x => x.Id == id && x.IsDelete == 0);
                if (order == null)
                {
                    continue;
                }
                if (order.OrderStatus == Enum.OrderStatusEnum.None || order.OrderStatus == Enum.OrderStatusEnum.TobeShipped)
                {
                    try
                    {
                        _orderStateService.CancelOrder(id);
                    }
                    catch (Exception ex)
                    {
                        // 并发下被支付/发货/关单抢先改掉：跳过该单，不影响其余（与批量取消的跳过口径一致）
                        skipped++;
                        logger.Warn($"删除订单时取消失败，已跳过该单：OrderNo={order.OrderNo} OrderId={id} 错误={ex.Message}");
                        continue;
                    }
                }
                count += Context.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder { IsDelete = 1 })
                    .Where(it => it.Id == id && it.IsDelete == 0)
                    .ExecuteCommand();
            }

            logger.Info($"批量删除订单完成：请求数={requestIds.Count} 实际删除={count} 跳过={skipped} Ids={string.Join(",", requestIds.Take(20))}");
            return count;
        }

        /// <summary>
        /// 修改平台备注
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateMerchantNote(OMSOrder model)
        {
            var result = Update(w => w.OrderNo == model.OrderNo, it => new OMSOrder()
            {
                MerchantNote = model.MerchantNote,
            });
            return result;
        }

        /// <summary>
        /// 导出订单管理
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<OMSOrderDto> ExportList(OMSOrderQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new OMSOrderDto()
                {
                }, true)
                .Mapper(it =>
                {
                    it.User = $"{it.AddressSnapshot?.UserName} {it.AddressSnapshot?.Phone}"; 
                })
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 导出代发货订单
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public async Task<List<DeliveryExpressDto>> ExportWaitDeliveryList(OMSOrderQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var predicate = Expressionable.Create<OMSOrder>();

            predicate = predicate.And(it => it.OrderStatus == Enum.OrderStatusEnum.TobeShipped);
            predicate = predicate.And(it => it.DeliveryStatus == Enum.DeliveryStatusEnum.NotDelivered);
            // 时间边界各自独立拼接：缺哪个就不加哪个，避免 `PayTime <= NULL` 恒假导致导出为空
            predicate = predicate.AndIF(parm.BeginCreateTime != null, it => it.PayTime >= parm.BeginCreateTime);
            predicate = predicate.AndIF(parm.EndCreateTime != null, it => it.PayTime <= parm.EndCreateTime);

            var response = await Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new DeliveryExpressDto()
                {
                    DeliveryCompany = it.DeliveryCompany,
                    DeliveryNo = it.DeliveryNo,
                    OrderNo = it.OrderNo,
                })
                .ToListAsync();

            return response;
        }

        /// <summary>
        /// 总销售额和订单数
        /// </summary>
        /// <returns></returns>
        public async Task<dynamic> GetTotalSales(OMSOrderQueryDto dto)
        {
            var result = await Queryable()
                // 口径：只统计「实际付过款且未作废」的订单
                // （PayTime 非空 = 已支付；排除已取消/已关闭与软删除，原先只按 PayTime 区间聚合会把作废单也算进销售额）
                .Where(o => o.IsDelete == 0
                    && o.PayTime != null
                    && o.OrderStatus != Enum.OrderStatusEnum.Cancel
                    && o.OrderStatus != Enum.OrderStatusEnum.Closed)
                .WhereIF(dto.BeginCreateTime != null, o => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, o => o.PayTime <= dto.EndCreateTime)
                .Select(o => new
                {
                    // 商品口径：按 TotalAmount（商品总额）聚合，运费不计入 GMV。
                    // 若改用 PayAmount（含运费），运费模板上线后销售额会随每单运费跳变。
                    TotalSales = SqlFunc.AggregateSum(o.TotalAmount),
                    OrderCount = SqlFunc.AggregateCount(o.Id)
                })
                .FirstAsync();
            return result;
        }

        /// <summary>
        /// 销售趋势（按天）
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<dynamic> GetSaleTreandByDay(OMSOrderQueryDto dto)
        {
            var trend = await Queryable()
            // 与销售总额口径一致：只统计已支付且未作废的订单
            .Where(o => o.IsDelete == 0
                && o.PayTime != null
                && o.OrderStatus != Enum.OrderStatusEnum.Cancel
                && o.OrderStatus != Enum.OrderStatusEnum.Closed)
            .WhereIF(dto.BeginCreateTime == null, o => o.PayTime >= DateTime.Now.AddDays(-7))
            .WhereIF(dto.BeginCreateTime != null, o => o.PayTime >= dto.BeginCreateTime)
            .WhereIF(dto.EndCreateTime != null, o => o.PayTime <= dto.EndCreateTime)
            .GroupBy(o => SqlFunc.ToDateShort(o.PayTime))
            .OrderBy(o => SqlFunc.ToDateShort(o.PayTime))
            .Select(o => new
            {
                Date = SqlFunc.ToDateShort(o.PayTime),
                // 与销售总额同为商品口径：按 TotalAmount 聚合，运费不计入 GMV
                TotalSales = SqlFunc.AggregateSum(o.TotalAmount),
                OrderCount = SqlFunc.AggregateCount(o.Id)
            })
            .ToListAsync();

            return trend;
        }

        /// <summary>
        /// 销售排名前10的商品
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<dynamic> GetSaleTopProduct(OMSOrderQueryDto dto)
        {
            var topProducts = await Context.Queryable<OMSOrderItem>()
                .InnerJoin<OMSOrder>((oi, o) => oi.OrderId == o.Id)
                // 与销售额口径保持一致：只统计已支付且未作废的订单
                .Where((oi, o) => o.IsDelete == 0
                    && o.PayTime != null
                    && o.OrderStatus != Enum.OrderStatusEnum.Cancel
                    && o.OrderStatus != Enum.OrderStatusEnum.Closed)
                .WhereIF(dto.BeginCreateTime != null, (oi, o) => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, (oi, o) => o.PayTime <= dto.EndCreateTime)
                .GroupBy((oi, o) => oi.ProductId)
                .OrderBy((oi, o) => SqlFunc.AggregateSum(oi.Quantity), OrderByType.Desc)
                .Select((oi, o) => new
                {
                    oi.ProductId,
                    TotalSold = SqlFunc.AggregateSum(oi.Quantity),
                    // TotalPrice 本身就是「单价 × 数量」（见 OMSOrderItem），再乘 Quantity 会算成「单价 × 数量²」
                    TotalSales = SqlFunc.AggregateSum(oi.TotalPrice)
                })
                .Take(10)
                .MergeTable()
                .LeftJoin<Product>((it, p) => it.ProductId == p.ProductId)
                .Select((it, p) => new
                {
                    p.ProductName,
                    it.TotalSold,
                    it.TotalSales
                })
                .ToListAsync();

            return topProducts;
        }

        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<OMSOrder> QueryExp(OMSOrderQueryDto parm)
        {
            var predicate = Expressionable.Create<OMSOrder>();
            // 无任何条件时默认只看最近 7 天；否则按传入的时间边界逐条拼接（缺哪个就不加哪个），
            // 避免 `CreateTime >= NULL`（只传结束时间）或 `<= NULL`（只传开始时间）导致静默查空。
            if (parm.OrderStatus == null && parm.BeginCreateTime == null && parm.EndCreateTime == null)
            {
                predicate = predicate.And(it => it.CreateTime >= DateTime.Now.AddDays(-7).ToShortDateString().ParseToDateTime());
            }
            else
            {
                predicate = predicate.AndIF(parm.BeginCreateTime != null, it => it.CreateTime >= parm.BeginCreateTime);
                predicate = predicate.AndIF(parm.EndCreateTime != null, it => it.CreateTime <= parm.EndCreateTime);
            }
            predicate = predicate.AndIF(parm.OrderNo.IsNotEmpty(), it => it.OrderNo == parm.OrderNo);
            predicate = predicate.AndIF(parm.MemberId != null, it => it.MemberId == parm.MemberId);
            predicate = predicate.AndIF(parm.OrderStatus != null, it => it.OrderStatus == parm.OrderStatus);

            //predicate = predicate.AndIF(parm.ConfirmStatus != null, it => it.ConfirmStatus == parm.ConfirmStatus);
            predicate = predicate.AndIF(parm.DeliveryNo.IsNotEmpty(), it => it.DeliveryNo == parm.DeliveryNo);
            predicate = predicate.And(it => it.IsDelete == 0);
            //待发货双条件查询
            predicate = predicate.AndIF(parm.OrderStatus == Enum.OrderStatusEnum.TobeShipped, it => it.DeliveryStatus == DeliveryStatusEnum.NotDelivered);

            return predicate;
        }
    }
}
