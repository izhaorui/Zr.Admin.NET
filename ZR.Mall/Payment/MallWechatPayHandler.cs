using ZR.Mall.Enum;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Payment;

namespace ZR.Mall.Payment
{
    /// <summary>
    /// 商城订单微信支付回调处理器：认领非租户续费单号（TO 前缀）的支付成功回调，
    /// 委托 OMSOrderService 按订单号完成支付状态流转。金额校验与幂等在订单服务内。
    /// </summary>
    [AppService(ServiceType = typeof(IWechatPayNotifyHandler), ServiceLifetime = LifeTime.Scoped)]
    public class MallWechatPayHandler : IWechatPayNotifyHandler
    {
        /// <summary>
        /// 租户续费流水单号前缀（SysTenantService.InsertTenantOrder 生成），归租户续费 handler 处理
        /// </summary>
        public const string TenantOrderPrefix = "TO";

        private readonly IOMSOrderStateService _orderService;

        public MallWechatPayHandler(IOMSOrderStateService orderService)
        {
            _orderService = orderService;
        }

        public bool CanHandle(string outTradeNo)
        {
            return !string.IsNullOrWhiteSpace(outTradeNo)
                && !outTradeNo.StartsWith(TenantOrderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public void HandlePaid(string orderNo, string transactionId, int totalFen)
        {
            _orderService.PayOrderByOrderNo(orderNo, PayTypeEnum.Wechat, transactionId, null, totalFen);
        }
    }
}
