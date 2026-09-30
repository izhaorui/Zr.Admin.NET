using Infrastructure.Cache;
using Infrastructure.Extensions;
using ZR.Common;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Services;

namespace ZR.Mall.Service
{
    /// <summary>
    /// C 端（游客 / 会员）订单交易实现：下单、支付入口、我的订单、会员取消。
    /// 只负责"用户侧"编排与归属校验；状态迁移委托给 <see cref="IOMSOrderStateService"/>，
    /// 与后台运营（OMSOrderService）物理隔离。
    /// </summary>
    [AppService(ServiceType = typeof(IFrontOrderService))]
    public class FrontOrderService : BaseService<OMSOrder>, IFrontOrderService
    {
        private readonly IOMSOrderStateService _orderStateService;

        public FrontOrderService(IOMSOrderStateService orderStateService)
        {
            _orderStateService = orderStateService;
        }

        /// <summary>
        /// 会员"我的订单"分页：会员本人下单(MemberId) + 同手机号的历史游客订单(GuestPhone) 合并。
        ///
        /// 手机号合并**只对真正的游客订单（MemberId 为空）生效**：会员已免短信验证码，
        /// 收货手机号不再经过归属校验，不能再用它认领订单 —— 否则会员 A 用会员 B 的手机号下单后，
        /// 该订单会出现在 B 的列表里。
        /// 历史游客订单（接入会员体系前产生，MemberId 为空）仍按手机号归属，行为不变。
        /// </summary>
        public PagedInfo<OMSOrderDto> GetMemberOrders(long memberId, string phone, MemberOrderQueryDto parm)
        {
            if (memberId <= 0)
            {
                throw new CustomException("会员身份无效");
            }
            parm ??= new MemberOrderQueryDto();

            // 逐段 Where 拼接，避免把 OR 混进 AND 组合时出现优先级问题
            var query = Queryable().Includes(x => x.Items).Where(it => it.IsDelete == 0);
            if (string.IsNullOrWhiteSpace(phone))
            {
                query = query.Where(it => it.MemberId == memberId);
            }
            else
            {
                // 会员订单只按 MemberId 认领；手机号仅用于认领 MemberId 为空的游客订单
                query = query.Where(it => it.MemberId == memberId || (it.MemberId == null && it.GuestPhone == phone));
            }
            query = query.WhereIF(parm.OrderStatus != null, it => it.OrderStatus == parm.OrderStatus);

            return query
                .OrderBy(it => it.Id, OrderByType.Desc)
                .ToPage<OMSOrder, OMSOrderDto>(parm);
        }

        /// <summary>
        /// 会员订单详情（校验归属，不属于该会员返回 null）
        /// </summary>
        public OMSOrder GetMemberOrder(long memberId, string phone, long id)
        {
            if (memberId <= 0)
            {
                throw new CustomException("会员身份无效");
            }
            var order = Queryable().Includes(x => x.Items).First(x => x.Id == id && x.IsDelete == 0);
            return order != null && BelongsToMember(order, memberId, phone) ? order : null;
        }

        /// <summary>
        /// 会员取消自己的订单（校验归属后复用状态内核的 CancelOrder：仅待付款/待发货可取消并回补库存）
        /// </summary>
        public int CancelMemberOrder(long memberId, string phone, long id)
        {
            if (memberId <= 0)
            {
                throw new CustomException("会员身份无效");
            }
            var order = Queryable().First(x => x.Id == id && x.IsDelete == 0);
            if (order == null || !BelongsToMember(order, memberId, phone))
            {
                throw new CustomException("订单不存在或无权限");
            }
            return _orderStateService.CancelOrder(id);
        }

        /// <summary>
        /// 下单防刷限流：IP 与手机号双维度配额（10 分钟窗口）。返回 false 时 msg 为提示。
        /// 备注：AspNetCoreRateLimit 已有全局 POST 1次/3s 兜底，此处为业务级配额。
        ///
        /// 用 CacheStore.Increment 做原子自增：Redis 开启时走 INCR（跨实例共享、原子），
        /// 否则回退本地内存的进程内锁（见 CacheStore）。
        /// 原实现是 CacheHelper 的「读-改-写」，并发下会丢计数；且本地内存不跨实例，
        /// 多实例部署时每个节点各算一份配额，限流形同虚设。
        /// </summary>
        public bool TryAcquireCreateQuota(string ip, string phone, out string msg)
        {
            msg = string.Empty;
            var cache = CacheStore.Default;
            // IP 维度：10 分钟内最多 10 次
            if (cache.Increment("mall_front_order_ip_" + ip, 10) > 10)
            {
                msg = "下单过于频繁，请稍后再试";
                return false;
            }
            // 手机号维度：10 分钟内最多 30 次
            if (cache.Increment("mall_front_order_phone_" + phone, 10) > 30)
            {
                msg = "该手机号下单过于频繁，请稍后再试";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 订单是否属于该会员：
        /// - 会员订单（MemberId 有值）**只认 MemberId**，任何手机号都不能替代 ——
        ///   会员免验证码后收货手机号不再校验归属，若仍允许"手机号匹配即放行"，
        ///   任何会员都能用他人手机号下单，并借此读到/取消他人订单；
        /// - 游客订单（MemberId 为空，含接入会员体系前的历史单）按手机号归属，行为不变。
        /// </summary>
        private static bool BelongsToMember(OMSOrder order, long memberId, string phone)
        {
            if (order.MemberId.HasValue)
            {
                return order.MemberId.Value == memberId;
            }
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }
            return order.GuestPhone == phone || order.AddressSnapshot?.Phone == phone;
        }

        /// <summary>
        /// 模拟支付（游客直接支付）：待付款 → 待发货，记录支付时间。
        /// 校验订单号+手机号双重匹配（游客无账号，手机号即身份锚点）后委托状态内核 PayOrderByOrderNo。
        /// 幂等：已支付（待发货及之后）直接返回成功，不重复变更。
        /// </summary>
        public OMSOrder PayOrder(string orderNo, string phone)
        {
            var order = Queryable().First(x => x.OrderNo == orderNo && x.IsDelete == 0);
            if (order == null || order.AddressSnapshot?.Phone != phone)
            {
                throw new CustomException("订单不存在或无权限");
            }
            return _orderStateService.PayOrderByOrderNo(orderNo);
        }

        /// <summary>
        /// 统计某身份在指定商品上「已累计购买」的件数（用于 Product.PurchaseLimit.TotalLimit 校验）。
        ///
        /// 计入范围（= 不累计已取消 / 已退款的订单）：
        /// - 订单状态排除 Cancel(已取消) 与 Closed(已关闭)；None/TobeShipped/Shipped/Completed 计入。
        ///   取消即释放额度 —— 否则用户取消一次就永久买不了，与"限购"的目的相反。
        /// - 退款状态排除 Refunded(已退款完成) 与 Returned(已退货完成)。
        ///   Refunding(退款中) / Returning(退货中) 仍计入，避免退款流程中被重复下单占用额度。
        ///
        /// 身份口径与"我的订单"保持一致（见 <see cref="BelongsToMember"/>）：
        /// 会员累计 = 该 MemberId 的订单 ∪ 同手机号的历史游客单。
        /// 否则同一个用户「先游客下单、再登录下单」即可绕过总限购。
        ///
        /// ⚠️ 并发：两次同时下单可能读到同一份已购件数而都通过校验。
        /// 与库存校验同性质（库存靠写阶段的乐观锁兜底），总限购被突破的损失也远小于超卖，暂不额外加锁。
        /// </summary>
        /// <param name="db">租户商城库客户端</param>
        /// <param name="productIds">本次下单涉及的商品（只统计这些，避免全表扫）</param>
        /// <param name="memberId">会员 ID；为 null 表示游客单，按手机号归属</param>
        /// <param name="phone">下单手机号（游客单的归属锚点；会员单用它并入同号游客单）</param>
        private static Dictionary<long, int> QueryBoughtQuantity(
            ISqlSugarClient db, List<long> productIds, long? memberId, string phone)
        {
            var result = new Dictionary<long, int>();
            if (productIds.Count == 0) return result;

            var query = db.Queryable<OMSOrderItem>()
                .InnerJoin<OMSOrder>((oi, o) => oi.OrderId == o.Id)
                .Where((oi, o) => o.IsDelete == 0
                    && productIds.Contains(oi.ProductId)
                    && o.OrderStatus != OrderStatusEnum.Cancel
                    && o.OrderStatus != OrderStatusEnum.Closed
                    && o.RefundStatus != RefundStatusEnum.Refunded
                    && o.RefundStatus != RefundStatusEnum.Returned);

            // ⚠️ 归属条件必须在 C# 层分支，不能写成 Where 里的三元表达式
            // （如 `(memberId.HasValue ? A : B)`）：
            // SqlSugar 会把它翻译成 SqlFunc.IIF，再套上 || 就直接报
            // "SqlFunc.IIF(arg1,arg2,arg3) ... argument do not support"，整个接口 500。
            // 另外也不能统一成 `MemberId == memberId || (...)`：
            // 游客单 memberId 为 null 时该条件会退化成「所有游客单」，把别人的额度也算进来。
            query = memberId.HasValue
                ? query.Where((oi, o) => o.MemberId == memberId || (o.MemberId == null && o.GuestPhone == phone))
                : query.Where((oi, o) => o.MemberId == null && o.GuestPhone == phone);

            var rows = query
                .GroupBy((oi, o) => oi.ProductId)
                .Select((oi, o) => new { oi.ProductId, Qty = SqlFunc.AggregateSum(oi.Quantity) })
                .ToList();

            foreach (var row in rows)
            {
                result[row.ProductId] = row.Qty;
            }
            return result;
        }

        /// <summary>
        /// 游客/C端下单
        /// 1. 校验商品/SKU 存在、上架、库存充足、未超每单限购与总限购（读阶段在事务外）
        /// 2. 计算订单金额（单价 × 数量，累加）
        /// 3. 写订单主表 + 订单项（商品/SKU/价格快照）
        /// 4. 乐观锁扣减库存、累加销量（防超卖）
        /// 身份：游客单不绑会员（MemberId 为空），手机号写入 GuestPhone 并同步到 AddressSnapshot.Phone 作为锚点。
        /// 幂等：同一 RequestId（或相同手机号+订单项签名）短窗口内只创建一次订单。
        /// 限购：SingleLimit（每单限购）与 TotalLimit（总限购件数）均已实现，
        ///       两者都按「商品」维度累计 —— 同一商品拆成多个 SKU 行也会合并后再判断。
        /// </summary>
        public OMSOrder CreateOrder(CreateOrderDto dto, long? memberId = null)
        {
            if (dto?.Items == null || dto.Items.Count == 0)
            {
                throw new CustomException("下单商品不能为空");
            }

            var db = Context;
            var dbDate = db.GetDate();

            // ---- 读阶段（事务外）：仅校验与快照计算，避免事务内持有读锁 ----
            var skuIds = dto.Items.Select(x => x.SkuId).Distinct().ToList();
            var skus = db.Queryable<Skus>().Where(s => skuIds.Contains(s.SkuId) && s.IsDelete == 0).ToList();
            if (skus.Count != skuIds.Count)
            {
                throw new CustomException("存在无效或已下架的商品规格");
            }
            // 预建字典，替换后续所有 List.First(...) 线性查找
            var skuMap = skus.ToDictionary(s => s.SkuId);

            var productIds = skus.Select(s => s.ProductId).Distinct().ToList();
            var products = db.Queryable<Product>().Where(p => productIds.Contains(p.ProductId) && p.IsDelete == 0).ToList();
            if (products.Count != productIds.Count)
            {
                throw new CustomException("存在无效或已下架的商品");
            }
            var productMap = products.ToDictionary(p => p.ProductId);

            // ---- 限购校验（按商品维度，不是按 SKU 行）----
            // ⚠️ 必须先按商品合并本次请求的件数再判断：同一商品可以拆成多个 SKU 行下单
            // （如"默认"×1 + "大"×1），逐行判断的话每行都不超限，合计却超了，等于没限。
            //
            // ⚠️ 限购必须在这里校验，不能只靠前端：
            // 前端商品详情页按 PurchaseLimit 限制「最多可购 N 件」，但那是纯前端约束 ——
            // 用户改一下下单 URL 的 quantity 就能传更大的值，库存校验也挡不住
            // （比如限购 1 件、库存 100 时可传 50）。
            var requestedByProduct = dto.Items
                .GroupBy(x => skuMap[x.SkuId].ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            // 仅当本次涉及的商品里有限购配置时才查历史，避免每次下单都多一次聚合查询
            var limitedProductIds = requestedByProduct
                .Select(r => productMap[r.ProductId])
                .Where(p => p.PurchaseLimit != null && p.PurchaseLimit.Limit)
                .Select(p => p.ProductId)
                .ToList();

            var boughtQty = limitedProductIds.Count > 0
                ? QueryBoughtQuantity(db, limitedProductIds, memberId, dto.Address?.Phone)
                : new Dictionary<long, int>();

            foreach (var req in requestedByProduct)
            {
                var product = productMap[req.ProductId];
                var limit = product.PurchaseLimit;
                if (limit == null || !limit.Limit) continue;

                if (limit.SingleLimit > 0 && req.Quantity > limit.SingleLimit)
                {
                    throw new CustomException($"商品「{product.ProductName}」每单限购 {limit.SingleLimit} 件");
                }

                if (limit.TotalLimit > 0)
                {
                    var bought = 0;
                    if (boughtQty.TryGetValue(product.ProductId, out var boughtQtyOfProduct))
                    {
                        bought = boughtQtyOfProduct;
                    }
                    if (bought + req.Quantity > limit.TotalLimit)
                    {
                        throw new CustomException(
                            $"商品「{product.ProductName}」限购 {limit.TotalLimit} 件（已购 {bought} 件），无法再下单");
                    }
                }
            }

            var orderItems = new List<OMSOrderItem>();
            decimal totalAmount = 0;
            foreach (var item in dto.Items)
            {
                var sku = skuMap[item.SkuId];
                var product = productMap[sku.ProductId];

                if (product.SaleStatus != Enum.SaleStatus.OnSale)
                {
                    throw new CustomException($"商品「{product.ProductName}」已下架，无法购买");
                }
                if (sku.Stock < item.Quantity)
                {
                    throw new CustomException($"商品「{product.ProductName}」库存不足（剩余 {sku.Stock}，需 {item.Quantity}）");
                }

                var unitPrice = sku.Price;
                var itemTotal = unitPrice * item.Quantity;
                totalAmount += itemTotal;

                orderItems.Add(new OMSOrderItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    ProductPic = ResolveItemPic(sku, product),
                    SkuId = sku.SkuId,
                    UnitPrice = unitPrice,
                    TotalPrice = itemTotal,
                    Quantity = item.Quantity,
                    SkuSpec = sku.SpecCombination,
                    AddTime = dbDate
                });
            }

            // ---- #3 幂等去重（事务外）----
            // 后端自动选择：Redis 已初始化（RedisServer:open=1）时用 Redis（跨实例共享），
            // 否则回退本地内存（见 CacheStore）。多实例部署务必开 Redis，否则幂等只在单进程内生效。
            var idemCache = CacheStore.Default;
            var idemKey = BuildCreateIdemKey(dto, totalAmount);
            var idemLockKey = idemKey + ":lock";

            // 已完成：命中则复用（仅当订单仍有效；已取消/关闭的不算，允许重新下单）
            var cachedNo = idemCache.Get<string>(idemKey);
            if (!string.IsNullOrEmpty(cachedNo))
            {
                var exist = Queryable().First(x => x.OrderNo == cachedNo && x.IsDelete == 0);
                if (exist != null && IsIdemOrderReusable(exist)) return exist;
                idemCache.Remove(idemKey); // 陈旧键（订单已取消/关闭/删除），清除后允许重建
            }

            // 原子占位：并发相同请求只有一个能进入建单，其余短暂等待并复用结果，避免重复下单。
            // 原实现是「读缓存 → 建单 → 写缓存」的 check-then-act，并发下会双双判定未命中而各建一单。
            if (!idemCache.TrySet(idemLockKey, 1, 2))
            {
                var reused = WaitReuseOrder(idemCache, idemKey);
                if (reused != null) return reused;
                throw new CustomException("订单正在处理中，请勿重复提交");
            }

            // ---- 写阶段（事务内）：订单主表 + 订单项 + 扣库存，整体原子 ----
            // 注意：必须用基类 UseTran（内部 try/catch + RollbackTran + 重新抛出异常），
            // 不能用 db.Ado.UseTran(Action)——后者会吞掉异常且不保证抛出，会导致
            // “库存不足等业务异常被静默忽略、order 仍为 null 后续 NRE、事务未回滚出现半成品”。
            OMSOrder order = null;
            try
            {
                // 抢到占位后再确认一次：上一个请求可能刚好完成并写入结果，避免重复建单
                var justCachedNo = idemCache.Get<string>(idemKey);
                if (!string.IsNullOrEmpty(justCachedNo))
                {
                    var justExist = Queryable().First(x => x.OrderNo == justCachedNo && x.IsDelete == 0);
                    if (justExist != null && IsIdemOrderReusable(justExist)) return justExist;
                }

                var tran = UseTran(() =>
                {
                    order = new OMSOrder
                    {
                        OrderNo = Common.OrderNoGenerator.Generate(),
                        MemberId = memberId, // C 端会员下单时绑定，用于"我的订单"归属
                        TotalAmount = totalAmount,
                        PayAmount = totalAmount,
                        OrderStatus = Enum.OrderStatusEnum.None, // 待付款：下单后进入模拟支付环节，支付成功才转待发货
                        DeliveryStatus = Enum.DeliveryStatusEnum.NotDelivered,
                        RefundStatus = Enum.RefundStatusEnum.None,
                        CreateTime = dbDate,
                        OrderNote = dto.OrderNote,
                        AddressSnapshot = dto.Address,
                        GuestPhone = dto.Address?.Phone // 身份锚点一等公民列，供 SQL 过滤查询
                    };

                    // 写入订单主表，返回自增 Id
                    var orderId = db.Insertable(order).ExecuteReturnIdentity();
                    order.Id = orderId;

                    // 写入订单项（回填 OrderId）
                    foreach (var oi in orderItems)
                    {
                        oi.OrderId = orderId;
                    }
                    db.Insertable(orderItems).ExecuteCommand();

                    // 原子扣减库存、累加销量（防超卖）；失败直接抛异常，整体事务回滚。
                    // 先把同一 SKU 的多行数量合并，再按数量分组批量 UPDATE，
                    // 替代原先「每个订单项一条 SQL」——买 N 种商品最多只发若干条（同数量合并为一条）。
                    // WHERE Stock >= qty 的条件更新本身即原子（数据库行锁），仍能防超卖；
                    // 不再强制 Version 相等（历史数据 Version 可能为 NULL 或漂移，会误报抢购）。
                    var deductPlan = dto.Items
                        .GroupBy(i => i.SkuId)
                        .Select(g => new { SkuId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                        .ToList();

                    foreach (var group in deductPlan.GroupBy(p => p.Quantity))
                    {
                        var qty = group.Key;
                        var groupSkuIds = group.Select(p => p.SkuId).ToList();
                        var affected = db.Updateable<Skus>()
                            .SetColumns(s => new Skus
                            {
                                Stock = s.Stock - qty,
                                LockStock = s.LockStock + qty,
                                SalesVolume = s.SalesVolume + qty,
                                Version = s.Version + 1
                            })
                            .Where(s => groupSkuIds.Contains(s.SkuId) && s.Stock >= qty)
                            .ExecuteCommand();
                        // 命中行数少于组内 SKU 数 → 至少一个 SKU 库存不足（或被并发扣走），整单回滚
                        if (affected < groupSkuIds.Count)
                        {
                            ThrowStockShortage(db, productMap, groupSkuIds, qty);
                        }
                    }

                    order.Items = orderItems;
                });

                // 双保险：即使某些异常未被抛出，事务失败也直接报错，绝不返回半成品订单
                if (tran == null || !tran.IsSuccess)
                {
                    throw new CustomException("下单失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
                }

                // 记录幂等键 -> 订单号（15 分钟内同单只创建一次）
                idemCache.Set(idemKey, order.OrderNo, 15);
                return order;
            }
            finally
            {
                // 释放占位：无论成功失败都清掉，失败后允许用户重试（Redis 键也带短过期兜底）
                idemCache.Remove(idemLockKey);
            }
        }

        /// <summary>
        /// 订单项商品图快照：优先 SKU 图，缺失时回退商品主图，再回退多图（逗号/换行分隔）首张。
        /// 很多商品只在商品级配主图、SKU 不单独配图，此前只取 sku.ImageUrl 会存下空串，
        /// 导致订单列表/详情无图可显示（后台页面前端只能自己兜占位图）。
        /// </summary>
        private static string ResolveItemPic(Skus sku, Product product)
        {
            if (!string.IsNullOrWhiteSpace(sku.ImageUrl))
            {
                return sku.ImageUrl;
            }
            if (!string.IsNullOrWhiteSpace(product.MainImage))
            {
                return product.MainImage;
            }
            return (product.ImageUrls ?? string.Empty)
                .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .FirstOrDefault(x => x.Length > 0) ?? string.Empty;
        }

        /// <summary>
        /// 计算下单幂等键：优先用前端传入的 RequestId，否则按“手机号+订单项+金额”签名
        /// </summary>
        private static string BuildCreateIdemKey(CreateOrderDto dto, decimal totalAmount)
        {
            if (!string.IsNullOrWhiteSpace(dto.RequestId))
            {
                return "mall_order_idem_req_" + dto.RequestId;
            }
            var itemsSig = string.Join("|", dto.Items.OrderBy(x => x.SkuId).Select(x => $"{x.SkuId}:{x.Quantity}"));
            return "mall_order_idem_sig_" + $"{dto.Address?.Phone}|{itemsSig}|{totalAmount}";
        }

        /// <summary>
        /// 幂等命中复用的订单是否仍有效：已取消/已关闭的订单不再复用，需允许用户重新下单。
        /// （缓存键只在成功后写入，且不随订单状态删除，故命中时须校验状态，否则会复用一笔已作废订单。）
        /// </summary>
        private static bool IsIdemOrderReusable(OMSOrder order)
        {
            return order.OrderStatus != Enum.OrderStatusEnum.Cancel
                && order.OrderStatus != Enum.OrderStatusEnum.Closed;
        }

        /// <summary>
        /// 并发相同请求时的短暂等待：另一请求正在建单，轮询其幂等结果，拿到则复用，
        /// 避免同一订单被并发创建两笔；超时未拿到由调用方提示"处理中"。
        /// </summary>
        private OMSOrder WaitReuseOrder(ICache cache, string idemKey)
        {
            for (var i = 0; i < 5; i++)
            {
                System.Threading.Thread.Sleep(200);
                var no = cache.Get<string>(idemKey);
                if (!string.IsNullOrEmpty(no))
                {
                    var exist = Queryable().First(x => x.OrderNo == no && x.IsDelete == 0);
                    if (exist != null && IsIdemOrderReusable(exist)) return exist;
                }
            }
            return null;
        }

        /// <summary>
        /// 批量扣库存命中行数不足时，精确定位库存不足的 SKU 并抛出业务异常（仅异常路径，允许逐条查询）。
        /// </summary>
        private static void ThrowStockShortage(ISqlSugarClient db, Dictionary<long, Product> productMap, List<long> skuIds, int qty)
        {
            foreach (var id in skuIds)
            {
                var sku = db.Queryable<Skus>().First(s => s.SkuId == id);
                if (sku == null || sku.Stock < qty)
                {
                    var name = sku != null && productMap.TryGetValue(sku.ProductId, out var p)
                        ? p.ProductName
                        : $"SKU {id}";
                    throw new CustomException($"商品「{name}」库存不足，请刷新后重试");
                }
            }
            throw new CustomException("部分商品库存不足，请刷新后重试");
        }
    }
}
