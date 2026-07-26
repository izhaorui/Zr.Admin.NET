using Infrastructure.Extensions;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 支付流水服务：记录预支付(prepaid)与支付成功(paid)流水，支撑多渠道、多次支付与对账。
    /// 状态机与订单流转在 OMSOrderService，本服务只负责支付记录的写与查询。
    /// </summary>
    [AppService(ServiceType = typeof(IOMSPaymentService))]
    public class OMSPaymentService : BaseService<OMSPayment>, IOMSPaymentService
    {
        /// <summary>
        /// 创建预支付流水（调起支付时写入，状态=Prepay）。用于微信 H5 等"先下单后回调"的通道。
        /// </summary>
        public OMSPayment CreatePrepay(OMSOrder order, PayTypeEnum payType, string prepayData)
        {
            var db = Context;
            var p = new OMSPayment
            {
                OrderNo = order.OrderNo,
                OrderId = order.Id,
                PayType = (int)payType,
                Amount = order.PayAmount,
                Status = (int)PaymentStatusEnum.Prepay,
                PrepayData = prepayData,
                CreateTime = db.GetDate()
            };
            p.Id = db.Insertable(p).ExecuteReturnIdentity();
            return p;
        }

        /// <summary>
        /// 写入/更新支付成功流水：若同订单同渠道存在 Prepay 记录则更新为 Paid，
        /// 否则直接插入 Paid 记录（模拟支付无预支付阶段）。幂等安全。
        /// </summary>
        public void UpsertPaid(OMSOrder order, PayTypeEnum payType, string channelTradeNo, DateTime payTime, string callbackRaw = null)
        {
            var db = Context;
            var prepay = db.Queryable<OMSPayment>()
                .First(x => x.OrderNo == order.OrderNo && x.PayType == (int)payType
                            && x.Status == (int)PaymentStatusEnum.Prepay && x.IsDelete == 0);
            if (prepay != null)
            {
                db.Updateable<OMSPayment>()
                    .SetColumns(it => new OMSPayment
                    {
                        Status = (int)PaymentStatusEnum.Paid,
                        ChannelTradeNo = channelTradeNo,
                        PayTime = payTime,
                        CallbackRaw = callbackRaw
                    })
                    .Where(it => it.Id == prepay.Id)
                    .ExecuteCommand();
            }
            else
            {
                db.Insertable(new OMSPayment
                {
                    OrderNo = order.OrderNo,
                    OrderId = order.Id,
                    PayType = (int)payType,
                    ChannelTradeNo = channelTradeNo,
                    Amount = order.PayAmount,
                    Status = (int)PaymentStatusEnum.Paid,
                    CreateTime = payTime,
                    PayTime = payTime,
                    CallbackRaw = callbackRaw
                }).ExecuteCommand();
            }
        }

        /// <summary>
        /// 分页查询支付流水（按订单号/渠道/状态/时间），仅统计未删除记录(IsDelete=0)。
        /// </summary>
        public PagedInfo<OMSPaymentDto> GetList(OMSPaymentQueryDto parm)
        {
            var predicate = QueryExp(parm);
            return Queryable()
                .Where(predicate.ToExpression())
                .OrderBy(it => it.Id, SqlSugar.OrderByType.Desc)
                .ToPage<OMSPayment, OMSPaymentDto>(parm);
        }

        private static Expressionable<OMSPayment> QueryExp(OMSPaymentQueryDto parm)
        {
            var predicate = Expressionable.Create<OMSPayment>();
            predicate = predicate.And(it => it.IsDelete == 0);
            predicate = predicate.AndIF(parm.OrderNo.IsNotEmpty(), it => it.OrderNo == parm.OrderNo);
            predicate = predicate.AndIF(parm.PayType != null, it => it.PayType == parm.PayType);
            predicate = predicate.AndIF(parm.Status != null, it => it.Status == parm.Status);
            predicate = predicate.AndIF(parm.ChannelTradeNo.IsNotEmpty(), it => it.ChannelTradeNo.Contains(parm.ChannelTradeNo));
            predicate = predicate.AndIF(parm.BeginCreateTime != null,
                it => it.CreateTime >= parm.BeginCreateTime && it.CreateTime <= parm.EndCreateTime);
            return predicate;
        }
    }
}
