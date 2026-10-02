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
            var now = db.GetDate();

            // 同一订单+渠道只保留一条"预支付"流水：用户反复点"去支付"时**更新**原记录，
            // 而不是每次插入新行。先按条件更新，避免「先查再插」在并发下各插一条。
            var updated = db.Updateable<OMSPayment>()
                .SetColumns(it => new OMSPayment
                {
                    Amount = order.PayAmount,
                    PrepayData = prepayData,
                    CreateTime = now
                })
                .Where(x => x.OrderNo == order.OrderNo && x.PayType == (int)payType
                            && x.Status == (int)PaymentStatusEnum.Prepay && x.IsDelete == 0)
                .ExecuteCommand();
            if (updated == 0)
            {
                db.Insertable(new OMSPayment
                {
                    OrderNo = order.OrderNo,
                    OrderId = order.Id,
                    PayType = (int)payType,
                    Amount = order.PayAmount,
                    Status = (int)PaymentStatusEnum.Prepay,
                    PrepayData = prepayData,
                    CreateTime = now
                }).ExecuteCommand();
            }

            // 并发下两个请求都可能插成功：只留 Id 最大的一条，其余软删，避免对账出现多条 Prepay。
            var rows = db.Queryable<OMSPayment>()
                .Where(x => x.OrderNo == order.OrderNo && x.PayType == (int)payType
                            && x.Status == (int)PaymentStatusEnum.Prepay && x.IsDelete == 0)
                .OrderBy(x => x.Id, OrderByType.Desc)
                .ToList();
            if (rows.Count == 0)
            {
                // 理论不可达（去重逻辑始终保留 Id 最大的一条），属防御分支：
                // 兜底再写一条并返回，保证调用方拿到的预支付流水非空（原实现此处 return null）。
                var fallback = new OMSPayment
                {
                    OrderNo = order.OrderNo,
                    OrderId = order.Id,
                    PayType = (int)payType,
                    Amount = order.PayAmount,
                    Status = (int)PaymentStatusEnum.Prepay,
                    PrepayData = prepayData,
                    CreateTime = now
                };
                fallback.Id = db.Insertable(fallback).ExecuteReturnBigIdentity();
                return fallback;
            }
            if (rows.Count > 1)
            {
                var dropIds = rows.Skip(1).Select(x => x.Id).ToList();
                db.Updateable<OMSPayment>()
                    .SetColumns(it => new OMSPayment { IsDelete = 1 })
                    .Where(it => dropIds.Contains(it.Id))
                    .ExecuteCommand();
            }
            return rows[0];
        }

        /// <summary>
        /// 写入/更新支付成功流水：若同订单同渠道存在 Prepay 记录则更新为 Paid，
        /// 否则直接插入 Paid 记录（模拟支付无预支付阶段）。幂等安全。
        /// </summary>
        public void UpsertPaid(OMSOrder order, PayTypeEnum payType, string channelTradeNo, DateTime payTime, string callbackRaw = null)
        {
            var db = Context;
            // 该订单该渠道下所有 Prepay 一并改为 Paid，避免并发预支付留下的第二条永远停在 Prepay。
            var affected = db.Updateable<OMSPayment>()
                .SetColumns(it => new OMSPayment
                {
                    Status = (int)PaymentStatusEnum.Paid,
                    ChannelTradeNo = channelTradeNo,
                    PayTime = payTime,
                    CallbackRaw = callbackRaw
                })
                .Where(x => x.OrderNo == order.OrderNo && x.PayType == (int)payType
                            && x.Status == (int)PaymentStatusEnum.Prepay && x.IsDelete == 0)
                .ExecuteCommand();
            if (affected > 0)
            {
                return;
            }

            var alreadyPaid = db.Queryable<OMSPayment>().Any(x => x.OrderNo == order.OrderNo
                && x.PayType == (int)payType && x.Status == (int)PaymentStatusEnum.Paid && x.IsDelete == 0);
            if (alreadyPaid)
            {
                return;
            }

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

        /// <summary>
        /// 关闭该订单仍处于预支付的流水。
        /// </summary>
        public void CloseUnpaid(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return;
            }
            Context.Updateable<OMSPayment>()
                .SetColumns(it => new OMSPayment { Status = (int)PaymentStatusEnum.Closed })
                .Where(x => x.OrderNo == orderNo && x.IsDelete == 0 && x.Status == (int)PaymentStatusEnum.Prepay)
                .ExecuteCommand();
        }

        /// <summary>
        /// 确认退款完成时调用：已支付流水标为已退款，残留预支付标为已关闭。
        /// 取消已支付订单时**不调用**（钱尚未退回，流水保持已支付，避免账实不符）。
        /// </summary>
        public void MarkRefunded(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
            {
                return;
            }
            Context.Updateable<OMSPayment>()
                .SetColumns(it => new OMSPayment { Status = (int)PaymentStatusEnum.Refunded })
                .Where(x => x.OrderNo == orderNo && x.IsDelete == 0 && x.Status == (int)PaymentStatusEnum.Paid)
                .ExecuteCommand();
            CloseUnpaid(orderNo);
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
            // 时间边界各自独立拼接：判断键必须与比较字段一致。
            // 原写法用 BeginCreateTime 判断却比较 EndCreateTime —— 只传开始时间会生成
            // `CreateTime <= NULL`（恒假、查空），只传结束时间则整个条件被跳过。
            predicate = predicate.AndIF(parm.BeginCreateTime != null, it => it.CreateTime >= parm.BeginCreateTime);
            predicate = predicate.AndIF(parm.EndCreateTime != null, it => it.CreateTime <= parm.EndCreateTime);
            return predicate;
        }
    }
}
