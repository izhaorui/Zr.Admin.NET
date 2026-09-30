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
    /// C 端交易（下单/我的订单/会员取消/游客支付）已移至 <see cref="FrontOrderService"/>。
    /// 本类只保留"运营视角"的只读查询与编排，不再直接改订单状态或库存。
    /// </summary>
    [AppService(ServiceType = typeof(IOMSOrderService))]
    public class OMSOrderService : BaseService<OMSOrder>, IOMSOrderService
    {
        private readonly IOMSOrderStateService _orderStateService;

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
            //订单退款：关闭订单 + 回补库存（简易版无支付，退款即退回库存）—— 状态迁移动交状态内核
            if (operType == 4)
            {
                return _orderStateService.RefundOrder(model);
            }
            return Update(model, true);
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
