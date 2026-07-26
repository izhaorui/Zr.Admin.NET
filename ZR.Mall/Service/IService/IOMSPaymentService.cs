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
        /// 创建预支付流水（状态=Prepay）
        /// </summary>
        OMSPayment CreatePrepay(OMSOrder order, PayTypeEnum payType, string prepayData);

        /// <summary>
        /// 写入/更新支付成功流水（幂等）
        /// </summary>
        void UpsertPaid(OMSOrder order, PayTypeEnum payType, string channelTradeNo, DateTime payTime, string callbackRaw = null);

        /// <summary>
        /// 分页查询支付流水（按订单号/渠道/状态/时间）
        /// </summary>
        PagedInfo<OMSPaymentDto> GetList(OMSPaymentQueryDto parm);
    }
}
