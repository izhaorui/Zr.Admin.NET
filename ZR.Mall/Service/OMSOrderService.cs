using Infrastructure.Extensions;
using ZR.Mall.Common;
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

            // 列表不返回表单值：列表用不上（只有详情要看），而它一次就是 10~20 单 × 每行 20 个字段的
            // JSON 快照 —— 既是报文浪费，也是批量带明文的风险。口径见 OrderFormValueHelper。
            OrderFormValueHelper.ClearItems(response.Result?
                .Where(x => x.Items != null)
                .SelectMany(x => x.Items));

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

            // 详情要展示买家填的表单值（发货/核对都靠它），敏感字段默认打码：
            // 非敏感字段（接收账号、备注、预约日期…）直接明文，省掉运营每单多点一次；
            // 需要明文看 sensitive 字段时走 formValuesRaw（独立权限 + 操作日志).
            OrderFormValueHelper.ApplyDisplayPolicy(response?.Items);

            return response;
        }

        /// <summary>
        /// 订单中**敏感字段**的完整值（揭示接口专用）。
        ///
        /// 与详情同一套判定，只把敏感字段换成明文：运营发货要的就是这个值
        /// （虚拟商品的接收手机号/接收账号就在其中）。
        /// 出口必须同时满足三件事，缺一不可：
        /// ① 独立权限 <c>oms:order:sensitive</c>；② 记操作日志（谁为哪单看过）；③ 只返回敏感字段，
        /// 非敏感字段本就是明文，再传一遍只会白扩大数据面。
        /// </summary>
        public List<OrderFormValue> GetSensitiveFormValues(long orderId)
        {
            var order = Queryable()
                .Includes(x => x.Items)
                .Where(x => x.Id == orderId && x.IsDelete == 0)
                .First();
            if (order == null)
            {
                throw new CustomException("订单不存在");
            }

            // Items 判空：Includes 在订单没有任何明细时不保证回填空集合，
            // 直接链式调用会 NRE —— 而这是个"只读接口居然 500"的典型来源。
            return (order.Items ?? new List<OMSOrderItem>())
                .SelectMany(x => OrderFormValueHelper.RevealSensitive(x.FormValues))
                .ToList();
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
        /// <remarks>
        /// 刻意**不加载行项**（<c>Items</c>）：导出是一单一行、页大小上限 10 万，
        /// 再把明细（含表单值快照）一起带出来，查询与内存都会上一个量级。
        /// 因此 <see cref="OMSOrderDto.FormValuesLabel"/> 不进 Excel 模板（后台列表与详情仍可见）。
        /// </remarks>
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

            FillFormValuesText(response.Result);

            return response;
        }

        /// <summary>
        /// 给导出结果补上「买家填写信息」列。
        ///
        /// 表单值挂在订单项上而导出是订单维度，故这里按导出的订单 Id **一次批量**取回所有订单项，
        /// 在内存里按 OrderId 聚合 —— 逐单回查就是 N+1 次往返（导出动辄上万行）。
        /// 值由 <see cref="OrderFormValueHelper.ToText"/> 脱敏：Excel 会被转发、传附件，
        /// 明文留在这里等于把敏感数据带出系统。
        /// </summary>
        private void FillFormValuesText(List<OMSOrderDto> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            var orderIds = rows.Select(x => x.Id).Distinct().ToList();
            var items = Context.Queryable<OMSOrderItem>()
                .Where(x => orderIds.Contains(x.OrderId))
                .Select(x => new { x.OrderId, x.FormValues })
                .ToList();

            var grouped = items
                .GroupBy(x => x.OrderId)
                .ToDictionary(g => g.Key, g => OrderFormValueHelper.ToText(
                    g.SelectMany(x => x.FormValues ?? new List<OrderFormValue>())));

            foreach (var row in rows)
            {
                if (grouped.TryGetValue(row.Id, out var text))
                {
                    row.FormValuesText = text;
                }
            }
        }

        /// <summary>
        /// 导出待发货订单（打单用）
        /// </summary>
        /// <remarks>
        /// 与导入模板分开：导入仍用 <see cref="DeliveryExpressDto"/>（三列，运营既有 Excel 习惯不动），
        /// 导出用 <see cref="DeliveryExportDto"/>（补齐收件人与商品明细，打单软件需要这些才能用）。
        ///
        /// 收件信息**明文不脱敏**（打单必需），只在本导出出口提供，不进日志、不进订单详情。
        /// </remarks>
        public async Task<List<DeliveryExportDto>> ExportWaitDeliveryList(OMSOrderQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var predicate = Expressionable.Create<OMSOrder>();

            predicate = predicate.And(it => it.OrderStatus == Enum.OrderStatusEnum.TobeShipped);
            predicate = predicate.And(it => it.DeliveryStatus == Enum.DeliveryStatusEnum.NotDelivered);
            // 时间边界各自独立拼接：缺哪个就不加哪个，避免 `PayTime <= NULL` 恒假导致导出为空
            predicate = predicate.AndIF(parm.BeginCreateTime != null, it => it.PayTime >= parm.BeginCreateTime);
            predicate = predicate.AndIF(parm.EndCreateTime != null, it => it.PayTime <= parm.EndCreateTime);

            // 取实体而非 Select 投影：AddressSnapshot 是 JSON 列，SqlSugar 在投影里按列名反序列化不稳
            // （同 GetFrontFormFields 的处理方式），故整单取回后在内存里组装。
            var orders = await Queryable()
                .Where(predicate.ToExpression())
                .ToListAsync();

            var orderIds = orders.Select(x => x.Id).ToList();
            var goodsByOrder = new Dictionary<long, string>();
            if (orderIds.Count > 0)
            {
                // 商品明细一次查齐、内存聚合：导出动辄上万行，逐单查订单项就是 N+1 次往返
                var allItems = await Context.Queryable<OMSOrderItem>()
                    .Where(x => orderIds.Contains(x.OrderId))
                    .Select(x => new { x.OrderId, x.ProductName, x.SkuSpec, x.Quantity })
                    .ToListAsync();

                goodsByOrder = allItems
                    .GroupBy(x => x.OrderId)
                    .ToDictionary(
                        g => g.Key,
                        // 形如「运动鞋（黑色;42）×2；袜子×1」，给打单软件一眼看清寄什么
                        g => string.Join("；", g.Select(i =>
                            string.IsNullOrWhiteSpace(i.SkuSpec)
                                ? $"{i.ProductName}×{i.Quantity}"
                                : $"{i.ProductName}（{i.SkuSpec}）×{i.Quantity}")));
            }

            return orders.Select(o =>
            {
                var addr = o.AddressSnapshot;
                return new DeliveryExportDto
                {
                    OrderNo = o.OrderNo,
                    ReceiverName = addr?.UserName,
                    ReceiverPhone = addr?.Phone,
                    ReceiverRegion = string.Join(" ", new[]
                    {
                        addr?.Province, addr?.City, addr?.District
                    }.Where(x => !string.IsNullOrWhiteSpace(x))),
                    DetailAddress = addr?.DetailAddress,
                    GoodsSummary = goodsByOrder.TryGetValue(o.Id, out var summary) ? summary : string.Empty,
                    DeliveryCompany = o.DeliveryCompany,
                    DeliveryNo = o.DeliveryNo
                };
            }).ToList();
        }

        /// <summary>
        /// 总销售额和订单数
        /// </summary>
        /// <returns></returns>
        public async Task<dynamic> GetTotalSales(OMSOrderQueryDto dto)
        {
            var result = await MallStatsScope.ValidOrders(Queryable())
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
            var trend = await MallStatsScope.ValidOrders(Queryable())
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
        /// 经营概览：销售统计页顶部的体温指标（客单价 / 退款率 / 售后率 / 形态占比 / 复购率）。
        ///
        /// 三条聚合 SQL 一次算完，不逐指标单查：
        /// ① 订单侧一次 GROUP BY 出订单数、销售额、实收、退款单数、形态分布；
        /// ② 会员维度一次 GROUP BY 出复购率（单条 SQL 内先按会员聚合计数，外层数"≥2 次"的人）；
        /// ③ 售后侧一次 COUNT 出售后单数。
        /// 比例全部在服务端算好返回：分母为 0 时给 0，不给前端出现 NaN/Infinity 的机会。
        /// </summary>
        /// <param name="dto">时间范围按 <b>支付时间</b>，与销售总额/趋势一致</param>
        public async Task<BusinessOverviewDto> GetBusinessOverview(OMSOrderQueryDto dto)
        {
            var orderStat = await MallStatsScope.ValidOrders(Queryable())
                .WhereIF(dto.BeginCreateTime != null, o => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, o => o.PayTime <= dto.EndCreateTime)
                .GroupBy(o => o.OrderType)
                .Select(o => new
                {
                    OrderType = o.OrderType,
                    OrderCount = SqlFunc.AggregateCount(o.Id),
                    TotalSales = SqlFunc.AggregateSum(o.TotalAmount),
                    TotalPaid = SqlFunc.AggregateSum(o.PayAmount),
                    // 发生过退款的订单数：按"有退款金额"判定，与 RefundAmount 同源，不会两处对不上
                    RefundOrderCount = SqlFunc.AggregateSum(
                        SqlFunc.IIF(o.RefundAmount > 0, 1, 0)),
                    RefundAmount = SqlFunc.AggregateSum(o.RefundAmount)
                })
                .ToListAsync();

            // 复购率：先按会员分组数出每人的单数，再统计"≥2 单"的会员数与总会员数
            var memberStat = await MallStatsScope.ValidOrders(Queryable())
                .WhereIF(dto.BeginCreateTime != null, o => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, o => o.PayTime <= dto.EndCreateTime)
                .Where(o => o.MemberId != null)
                .GroupBy(o => o.MemberId)
                .Select(o => new { OrderCount = SqlFunc.AggregateCount(o.Id) })
                .ToListAsync();

            // 售后单数：按申请时间落在区间内统计（售后是独立单据，不能靠订单 PayTime 反推）
            var afterSaleCount = await Context.Queryable<OMSAfterSale>()
                .Where(x => x.IsDelete == 0)
                .WhereIF(dto.BeginCreateTime != null, x => x.ApplyTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, x => x.ApplyTime <= dto.EndCreateTime)
                .CountAsync();

            var orderCount = orderStat.Sum(x => x.OrderCount);
            var totalSales = orderStat.Sum(x => x.TotalSales);
            var totalPaid = orderStat.Sum(x => x.TotalPaid);
            var refundOrderCount = orderStat.Sum(x => x.RefundOrderCount);
            var refundAmount = orderStat.Sum(x => x.RefundAmount);
            var virtualCount = orderStat.Where(x => x.OrderType == Enum.ProductTypeEnum.Virtual).Sum(x => x.OrderCount);
            var memberCount = memberStat.Count;
            var repurchaseCount = memberStat.Count(x => x.OrderCount >= 2);

            return new BusinessOverviewDto
            {
                OrderCount = orderCount,
                TotalSales = totalSales,
                TotalPaid = totalPaid,
                AvgOrderAmount = orderCount > 0 ? Round(totalPaid / orderCount) : 0m,
                RefundOrderCount = refundOrderCount,
                RefundAmount = refundAmount,
                RefundAmountRate = totalPaid > 0 ? Round(refundAmount / totalPaid) : 0m,
                RefundOrderRate = orderCount > 0 ? Round((decimal)refundOrderCount / orderCount) : 0m,
                AfterSaleCount = afterSaleCount,
                AfterSaleRate = orderCount > 0 ? Round((decimal)afterSaleCount / orderCount) : 0m,
                PhysicalOrderCount = orderCount - virtualCount,
                VirtualOrderCount = virtualCount,
                VirtualOrderRate = orderCount > 0 ? Round(virtualCount) / orderCount : 0m,
                MemberOrderCount = memberCount,
                RepurchaseRate = memberCount > 0 ? Round(repurchaseCount) / memberCount : 0m
            };
        }

        /// <summary>
        /// 退款 Top10 商品：决定"要不要下架某个商品"的直接依据。
        ///
        /// <b>口径与销售统计故意不同</b>，且这个不同是数据模型决定的、绕不开：
        /// 退款金额是**订单级**字段（<c>OMSOrder.RefundAmount</c>），订单项上没有退款额，
        /// 所以"某个商品退了多少"在部分退款场景下**无法精确拆分** ——
        /// 强行按订单项摊会重复计算（一笔退款会摊到该单所有商品上）。
        /// 故这里给的是两个口径都站得住的指标：
        /// <list type="bullet">
        /// <item><c>RefundOrderCount</c>：该商品出现在多少笔退款订单里（准确，内存按 订单+商品 去重）；</item>
        /// <item><c>RefundSales</c>：这些退款订单里该商品的**销售额**（规模参考，不是退款额 —— 字段名里刻意不写 Refund）。</item>
        /// </list>
        ///
        /// 订单侧过滤只排未付款与已取消，<b>不能</b>排除已退款（<c>Closed</c>）：
        /// 退款单大多已被整单退款置为 Closed，按有效订单口径过滤会让结果恒为空。
        /// </summary>
        public async Task<List<RefundTopProductDto>> GetRefundTopProduct(OMSOrderQueryDto dto)
        {
            // 退款订单里的商品行（只取 3 列，行数 = 退款订单的行数，很小）
            var refundLines = await Context.Queryable<OMSOrderItem>()
                .InnerJoin<OMSOrder>((oi, o) => oi.OrderId == o.Id)
                .Where((oi, o) => o.IsDelete == 0
                    && o.PayTime != null
                    && o.OrderStatus != Enum.OrderStatusEnum.Cancel
                    && o.RefundAmount > 0)
                .WhereIF(dto.BeginCreateTime != null, (oi, o) => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, (oi, o) => o.PayTime <= dto.EndCreateTime)
                .Select((oi, o) => new { oi.ProductId, oi.OrderId, oi.TotalPrice })
                .ToListAsync();

            if (refundLines.Count == 0)
            {
                return new List<RefundTopProductDto>();
            }

            // 内存按「订单 + 商品」去重后聚合：同一订单里同一商品可能有多行（多规格），
            // 直接 COUNT 会把笔数算重；去重后每笔订单对该商品只计一次
            var refundByProduct = refundLines
                .GroupBy(x => new { x.ProductId, x.OrderId })
                .Select(g => new { g.Key.ProductId, TotalPrice = g.Sum(x => x.TotalPrice) })
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, RefundOrderCount = g.Count(), RefundSales = g.Sum(x => x.TotalPrice) })
                .OrderByDescending(x => x.RefundSales)
                .Take(10)
                .ToList();

            // 分母：该商品在区间内的总销售额（有效订单口径，与销售统计同源）
            var productIds = refundByProduct.Select(x => x.ProductId).ToList();
            var totalByProduct = await Context.Queryable<OMSOrderItem>()
                .InnerJoin<OMSOrder>((oi, o) => oi.OrderId == o.Id)
                .Where((oi, o) => o.IsDelete == 0
                    && o.PayTime != null
                    && o.OrderStatus != Enum.OrderStatusEnum.Cancel
                    && o.OrderStatus != Enum.OrderStatusEnum.Closed
                    && productIds.Contains(oi.ProductId))
                .WhereIF(dto.BeginCreateTime != null, (oi, o) => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.EndCreateTime != null, (oi, o) => o.PayTime <= dto.EndCreateTime)
                .GroupBy((oi, o) => oi.ProductId)
                .Select((oi, o) => new { oi.ProductId, TotalSales = SqlFunc.AggregateSum(oi.TotalPrice) })
                .ToListAsync();

            var names = await Context.Queryable<Product>()
                .Where(p => productIds.Contains(p.ProductId))
                .Select(p => new { p.ProductId, p.ProductName })
                .ToListAsync();

            var nameMap = names.ToDictionary(x => x.ProductId, x => x.ProductName);
            var totalMap = totalByProduct.ToDictionary(x => x.ProductId, x => x.TotalSales);

            return refundByProduct.Select(x =>
            {
                var totalSales = totalMap.TryGetValue(x.ProductId, out var total) ? total : 0m;
                nameMap.TryGetValue(x.ProductId, out var name);
                return new RefundTopProductDto
                {
                    ProductId = x.ProductId,
                    ProductName = name,
                    RefundOrderCount = x.RefundOrderCount,
                    RefundSales = x.RefundSales,
                    TotalSales = totalSales,
                    RefundRate = totalSales > 0 ? Round(x.RefundSales / totalSales) : 0m
                };
            }).ToList();
        }

        /// <summary>统计比率统一保留 4 位小数：前端只负责显示，不做二次换算</summary>
        private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);


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
