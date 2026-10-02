using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 支付流水服务接口
    /// </summary>
    public interface IOMSPaymentService : IBaseService<OMSPayment>
    {
        /// <summary>
        /// 创建或更新预支付流水（状态=Prepay），并返回该流水。
        /// 同一订单+渠道只保留一条：已存在则更新凭证，不存在则插入；并发插入的多余记录会被软删。
        /// </summary>
        OMSPayment CreatePrepay(OMSOrder order, PayTypeEnum payType, string prepayData);

        /// <summary>
        /// 写入/更新支付成功流水（幂等）
        /// </summary>
        void UpsertPaid(OMSOrder order, PayTypeEnum payType, string channelTradeNo, DateTime payTime, string callbackRaw = null);

        /// <summary>
        /// 关闭该订单仍处于预支付的流水（取消未支付订单时调用）。
        /// </summary>
        void CloseUnpaid(string orderNo);

        /// <summary>
        /// 确认退款完成时调用：已支付流水标为已退款，残留预支付标为已关闭。
        /// 取消已支付订单时**不调用**（此时钱还未退，流水应保持已支付）；渠道侧退款由人工完成，这里只改对账状态。
        /// </summary>
        void MarkRefunded(string orderNo);

        /// <summary>
        /// 分页查询支付流水（按订单号/渠道/状态/时间）
        /// </summary>
        PagedInfo<OMSPaymentDto> GetList(OMSPaymentQueryDto parm);
    }
}
