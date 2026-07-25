using Infrastructure.Extensions;
using SqlSugar.IOC;
using ZR.Common;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Services;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 订单管理Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IOMSOrderService))]
    public class OMSOrderService : BaseService<OMSOrder>, IOMSOrderService
    {
        private ISkusService _shopSkusService;
        private readonly ISmsCodeLogService _smsCodeLogService;

        public OMSOrderService(ISkusService shopSkusService, ISmsCodeLogService smsCodeLogService)
        {
            _shopSkusService = shopSkusService;
            _smsCodeLogService = smsCodeLogService;
            // 不再固定 MallDb：商城实体按其 [Tenant("MallDb")] 属性（非多租户模式）或
            // 当前解析租户（多租户模式，含子域名解析的匿名请求）路由到对应库，
            // 由 BaseService 默认逻辑处理，保证匿名写入与登录读取落到同一租户库。
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
        /// 查询未发货的订单数
        /// </summary>
        /// <returns></returns>
        public int NotDelivereOrder()
        {
            return Queryable()
                .Where(f => f.OrderStatus == Enum.OrderStatusEnum.TobeShipped && f.DeliveryStatus == Enum.DeliveryStatusEnum.NotDelivered)
                .Count();
        }

        /// <summary>
        /// 订单各状态计数统计（用于列表顶部概览卡）。
        /// 一次 GroupBy 统计全部状态，比多次 COUNT 高效。
        /// 仅统计未删除订单(IsDelete=0)。
        /// </summary>
        public Dictionary<int, int> GetOrderStatusStats()
        {
            return Queryable()
                .Where(f => f.IsDelete == 0)
                .GroupBy(f => f.OrderStatus)
                .Select(f => new { Status = (int)f.OrderStatus, Count = SqlFunc.AggregateCount(f.Id) })
                .ToList()
                .ToDictionary(x => x.Status, x => x.Count);
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
        /// 游客/C端下单
        /// 1. 校验商品/SKU 存在、上架、库存充足（读阶段在事务外）
        /// 2. 计算订单金额（单价 × 数量，累加）
        /// 3. 写订单主表 + 订单项（商品/SKU/价格快照）
        /// 4. 乐观锁扣减库存、累加销量（防超卖）
        /// 游客下单 UserId 为 null，手机号记录在 AddressSnapshot.Phone 作为身份锚点。
        /// 幂等：同一 RequestId（或相同手机号+订单项签名）短窗口内只创建一次订单。
        /// </summary>
        public OMSOrder CreateOrder(CreateOrderDto dto)
        {
            var db = Context;
            var dbDate = db.GetDate();

            // ---- 读阶段（事务外）：仅校验与快照计算，避免事务内持有读锁 ----
            var skuIds = dto.Items.Select(x => x.SkuId).Distinct().ToList();
            var skus = db.Queryable<Skus>().Where(s => skuIds.Contains(s.SkuId) && s.IsDelete == 0).ToList();
            if (skus.Count != skuIds.Count)
            {
                throw new CustomException("存在无效或已下架的商品规格");
            }

            var productIds = skus.Select(s => s.ProductId).Distinct().ToList();
            var products = db.Queryable<Product>().Where(p => productIds.Contains(p.ProductId) && p.IsDelete == 0).ToList();
            if (products.Count != productIds.Count)
            {
                throw new CustomException("存在无效或已下架的商品");
            }

            var orderItems = new List<OMSOrderItem>();
            decimal totalAmount = 0;
            foreach (var item in dto.Items)
            {
                var sku = skus.First(s => s.SkuId == item.SkuId);
                var product = products.First(p => p.ProductId == sku.ProductId);

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
                    ProductPic = sku.ImageUrl,
                    SkuId = sku.SkuId,
                    UnitPrice = unitPrice,
                    TotalPrice = itemTotal,
                    Quantity = item.Quantity,
                    SkuSpec = sku.SpecCombination,
                    AddTime = dbDate
                });
            }

            // ---- #3 幂等去重（事务外短窗口）----
            var idemKey = BuildCreateIdemKey(dto, totalAmount);
            var cachedNo = CacheHelper.Get(idemKey) as string;
            if (!string.IsNullOrEmpty(cachedNo))
            {
                var exist = Queryable().Includes(x => x.Items).First(x => x.OrderNo == cachedNo && x.IsDelete == 0);
                if (exist != null) return exist;
            }

            // ---- 写阶段（事务内）：订单主表 + 订单项 + 扣库存，整体原子 ----
            // 注意：必须用基类 UseTran（内部 try/catch + RollbackTran + 重新抛出异常），
            // 不能用 db.Ado.UseTran(Action)——后者会吞掉异常且不保证抛出，会导致
            // “库存不足等业务异常被静默忽略、order 仍为 null 后续 NRE、事务未回滚出现半成品”。
            OMSOrder order = null;
            var tran = UseTran(() =>
            {
                order = new OMSOrder
                {
                    OrderNo = ZR.Mall.Common.OrderNoGenerator.Generate(),
                    UserId = null, // 游客匿名下单
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
                // 说明：WHERE Stock >= quantity 的条件更新本身即为原子操作（数据库行锁），已能防超卖；
                // 不再强制 Version 相等——历史 SKU 的 Version 列可能为 NULL 或与读取值漂移，
                // 会导致条件永远不匹配、误报“已被抢购”。仍递增 Version 供其它乐观锁场景使用。
                foreach (var item in dto.Items)
                {
                    var sku = skus.First(s => s.SkuId == item.SkuId);
                    var affected = db.Updateable<Skus>()
                        .SetColumns(s => new Skus
                        {
                            Stock = s.Stock - item.Quantity,
                            SalesVolume = s.SalesVolume + item.Quantity,
                            Version = s.Version + 1
                        })
                        .Where(s => s.SkuId == item.SkuId && s.Stock >= item.Quantity)
                        .ExecuteCommand();
                    if (affected <= 0)
                    {
                        throw new CustomException($"商品「{products.First(p => p.ProductId == sku.ProductId).ProductName}」库存不足，请刷新后重试");
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
            CacheHelper.SetCache(idemKey, order.OrderNo, 15);
            return order;
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
        /// 模拟支付：待付款 → 待发货，记录支付时间。
        /// 校验订单号+手机号双重匹配（游客无账号，手机号即身份锚点）。
        /// 幂等：已支付（待发货及之后）直接返回成功，不重复变更。
        /// 后期接入真实支付（微信/支付宝）时，将此方法改为支付回调里调用即可，状态机不变。
        /// </summary>
        public OMSOrder PayOrder(string orderNo, string phone)
        {
            var order = Queryable().First(x => x.OrderNo == orderNo && x.IsDelete == 0);
            if (order == null || order.AddressSnapshot?.Phone != phone)
            {
                throw new CustomException("订单不存在或无权限");
            }
            // 幂等：已支付直接返回
            if (order.OrderStatus == Enum.OrderStatusEnum.TobeShipped ||
                order.OrderStatus == Enum.OrderStatusEnum.Shipped ||
                order.OrderStatus == Enum.OrderStatusEnum.Completed)
            {
                return order;
            }
            if (order.OrderStatus != Enum.OrderStatusEnum.None)
            {
                throw new CustomException("该订单已取消或已关闭，无法支付");
            }

            var dbDate = Context.GetDate();
            // 条件更新：仅待付款状态可支付成功（防并发重复支付/支付与取消竞态）
            var affected = Context.Updateable<OMSOrder>()
                .SetColumns(it => new OMSOrder
                {
                    OrderStatus = Enum.OrderStatusEnum.TobeShipped,
                    PayTime = dbDate
                })
                .Where(it => it.OrderNo == orderNo && it.OrderStatus == Enum.OrderStatusEnum.None && it.IsDelete == 0)
                .ExecuteCommand();
            if (affected <= 0)
            {
                throw new CustomException("支付失败：订单状态已变更，请刷新后重试");
            }
            order.OrderStatus = Enum.OrderStatusEnum.TobeShipped;
            order.PayTime = dbDate;
            return order;
        }

        /// <summary>
        /// 修改订单管理
        /// </summary>
        /// <param name="operType"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateOMSOrder(int operType, OMSOrder model)
        {
            //修改商家备注
            if (operType == 2)
            {
                return UpdateMerchantNote(model);
            }
            //修改地址
            if (operType == 3)
            {
                var result = Update(w => w.OrderNo == model.OrderNo, it => new OMSOrder()
                {
                    AddressSnapshot = model.AddressSnapshot,
                });
                return result;
            }
            //订单退款：关闭订单 + 回补库存（简易版无支付，退款即退回库存）
            if (operType == 4)
            {
                return RefundOrder(model);
            }
            return Update(model, true);
        }

        /// <summary>
        /// 关闭超时未支付的待付款订单并回补库存（定时任务 Job_ClosePendingOrder 调用）。
        /// 用条件更新（仅待付款且超时）保证幂等：重复执行只会影响仍处于待付款且超时的单，
        /// 已取消的单不再命中，且回补仅针对“本次实际变更”的订单，不会重复回补库存。
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
                var now = db.GetDate();
                // 1) 原子将超时待付款单置为取消（条件更新，重复执行 affected=0 不重复处理）
                var affected = db.Updateable<OMSOrder>()
                    .SetColumns(it => new OMSOrder
                    {
                        OrderStatus = Enum.OrderStatusEnum.Cancel,
                        CancelTime = now
                    })
                    .Where(x => x.OrderStatus == Enum.OrderStatusEnum.None && x.IsDelete == 0 && x.CreateTime < expireTime)
                    .ExecuteCommand();
                if (affected <= 0) return;

                // 2) 仅回补“本次刚取消”的订单（CancelTime 在极近窗口内），避免重复回补
                var cancelled = db.Queryable<OMSOrder>()
                    .Where(x => x.OrderStatus == Enum.OrderStatusEnum.Cancel
                                && x.IsDelete == 0
                                && x.CreateTime < expireTime
                                && x.CancelTime >= now.AddSeconds(-10))
                    .Select(x => x.Id)
                    .ToList();
                foreach (var id in cancelled)
                {
                    RestoreStock(id);
                }
                closed = affected;
            });
            if (tran == null || !tran.IsSuccess)
            {
                throw new CustomException("超时订单清理失败" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            return closed;
        }

        /// <summary>
        /// 取消订单（游客/后台通用）：仅“待发货”可取消，回补库存
        /// </summary>
        public int CancelOrder(long id)
        {
            var db = Context;
            var dbDate = db.GetDate();
            var order = Queryable().Includes(x => x.Items).First(x => x.Id == id && x.IsDelete == 0);
            if (order == null)
            {
                throw new CustomException("订单不存在");
            }
            if (order.OrderStatus != Enum.OrderStatusEnum.None && order.OrderStatus != Enum.OrderStatusEnum.TobeShipped)
            {
                throw new CustomException("仅待付款/待发货状态的订单可取消");
            }

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
                throw new CustomException("取消订单失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
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

                    Update(x => x.Id == id, it => new OMSOrder
                    {
                        OrderStatus = Enum.OrderStatusEnum.Cancel,
                        CancelTime = dbDate
                    });
                    RestoreStock(order.Id);
                    count++;
                }
            });
            if (tran == null || !tran.IsSuccess)
            {
                throw new CustomException("批量取消订单失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
            return count;
        }

        /// <summary>
        /// 退款：关闭订单 + 回补库存（简易版无支付，退款即退回库存）
        /// </summary>
        private int RefundOrder(OMSOrder model)
        {
            var db = Context;
            var dbDate = db.GetDate();
            var order = Queryable().Includes(x => x.Items).First(x => x.OrderNo == model.OrderNo && x.IsDelete == 0);
            if (order == null)
            {
                throw new CustomException("订单不存在");
            }
            if (order.OrderStatus == Enum.OrderStatusEnum.Cancel || order.OrderStatus == Enum.OrderStatusEnum.Closed)
            {
                throw new CustomException("该订单状态不可退款");
            }

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
                throw new CustomException("退款失败，请重试" + (tran?.ErrorMessage != null ? $"（{tran.ErrorMessage}）" : ""));
            }
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
                Context.Updateable<Skus>()
                    .SetColumns(s => new Skus { Stock = s.Stock + it.Quantity })
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
            var dbDate = Context.GetDate();
            if (model.DeliveryStatus != DeliveryStatusEnum.NotDelivered)
            {
                return -1;//已发货
            }
            if (model.AddressSnapshot == null)
            {
                return -2; // 地址信息不能为空
            }
            // 仅“待发货”订单可发货，防止把待付款/已取消/已关闭/已发货单误改为已发货
            if (model.OrderStatus != OrderStatusEnum.TobeShipped)
            {
                return -3; // 非待发货状态，不可发货
            }
            var result = await UpdateAsync(w => w.OrderNo == model.OrderNo, it => new OMSOrder()
            {
                DeliveryCompany = model.DeliveryCompany,
                DeliveryNo = model.DeliveryNo,
                OrderStatus = OrderStatusEnum.Shipped, // 已发货
                DeliveryStatus = DeliveryStatusEnum.Delivering, // 已发货
                ShipTime = dbDate
            });
            // 发货成功 → 短信通知买家（游客无账号，走短信；失败不阻断发货）
            if (result > 0)
            {
                try
                {
                    var phone = await Queryable()
                        .Where(w => w.OrderNo == model.OrderNo)
                        .Select(w => w.GuestPhone)
                        .FirstAsync();
                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        var content = $"您的订单({model.OrderNo})已发货，{model.DeliveryCompany} 运单号 {model.DeliveryNo}，请留意查收。";
                        _smsCodeLogService.SendSmsNotice(phone, content, 6);
                    }
                }
                catch (System.Exception ex)
                {
                    // 通知异常仅记录，不影响发货结果
                    Log.WriteLine(ConsoleColor.Yellow, $"[OMSOrder] 发货通知发送失败(OrderNo={model.OrderNo}): {ex.Message}");
                }
            }
            return result;
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
            predicate = predicate.And(it => it.PayTime >= parm.BeginCreateTime && it.PayTime <= parm.EndCreateTime);

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
                //.Where(o => o.OrderStatus == Enum.OrderStatusEnum.Completed)
                .WhereIF(dto.BeginCreateTime != null, o => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.BeginCreateTime != null, o => o.PayTime <= dto.EndCreateTime)
                .Select(o => new
                {
                    TotalSales = SqlFunc.AggregateSum(o.PayAmount),
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
            .WhereIF(dto.BeginCreateTime == null, o => o.PayTime >= DateTime.Now.AddDays(-7))
            .WhereIF(dto.BeginCreateTime != null, o => o.PayTime >= dto.BeginCreateTime)
            .WhereIF(dto.BeginCreateTime != null, o => o.PayTime <= dto.EndCreateTime)
            .GroupBy(o => SqlFunc.ToDateShort(o.PayTime))
            .OrderBy(o => SqlFunc.ToDateShort(o.PayTime))
            .Select(o => new
            {
                Date = SqlFunc.ToDateShort(o.PayTime),
                TotalSales = SqlFunc.AggregateSum(o.PayAmount),
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
                .WhereIF(dto.BeginCreateTime != null, (oi, o) => o.PayTime >= dto.BeginCreateTime)
                .WhereIF(dto.BeginCreateTime != null, (oi, o) => o.PayTime <= dto.EndCreateTime)
                .GroupBy((oi, o) => oi.ProductId)
                .OrderBy((oi, o) => SqlFunc.AggregateSum(oi.Quantity), OrderByType.Desc)
                .Select((oi, o) => new
                {
                    oi.ProductId,
                    TotalSold = SqlFunc.AggregateSum(oi.Quantity),
                    TotalSales = SqlFunc.AggregateSum(oi.Quantity * oi.TotalPrice)
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
            if (parm.OrderStatus == null && parm.BeginCreateTime == null)
            {
                predicate = predicate.And(it => it.CreateTime >= DateTime.Now.AddDays(-7).ToShortDateString().ParseToDateTime());
            }
            else
            {
                predicate = predicate.AndIF(parm.EndCreateTime != null, it => it.CreateTime >= parm.BeginCreateTime && it.CreateTime <= parm.EndCreateTime);
            }
            predicate = predicate.AndIF(parm.OrderNo.IsNotEmpty(), it => it.OrderNo == parm.OrderNo);
            predicate = predicate.AndIF(parm.UserId != null, it => it.UserId == parm.UserId);
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