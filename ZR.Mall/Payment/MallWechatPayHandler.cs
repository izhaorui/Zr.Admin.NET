using ZR.Mall.Enum;
using ZR.Mall.Service.IService;
using Infrastructure.Payment;

namespace ZR.Mall.Payment
{
    /// <summary>
    /// 商城订单微信支付回调处理器：认领商城订单号（纯数字雪花 ID），
    /// 委托订单状态内核按订单号完成支付流转。金额校验与幂等在订单服务内。
    /// </summary>
    [AppService(ServiceType = typeof(IWechatPayNotifyHandler), ServiceLifetime = LifeTime.Scoped)]
    public class MallWechatPayHandler : IWechatPayNotifyHandler
    {
        /// <summary>
        /// 租户续费流水单号前缀（SysTenantService.InsertTenantOrder 生成），归租户续费 handler 处理。
        /// 保留为公开常量，作为单号前缀约定的单一出处。
        /// </summary>
        public const string TenantOrderPrefix = "TO";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IOMSOrderStateService _orderService;

        public MallWechatPayHandler(IOMSOrderStateService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// 认领规则：商城订单号是雪花 ID（纯数字，见 ZR.Mall.Common.OrderNoGenerator）。
        /// 用"纯数字"精确认领，而非"非 TO 前缀即商城"的兜底 —— 后者会把其它业务日后新增的
        /// 任何单号前缀一并认领，导致"订单不存在"并把微信回调打成失败反复重推。
        /// </summary>
        public bool CanHandle(string outTradeNo)
        {
            return !string.IsNullOrWhiteSpace(outTradeNo) && outTradeNo.All(char.IsAsciiDigit);
        }

        public void HandlePaid(string orderNo, string transactionId, int totalFen, string callbackRaw = null)
        {
            logger.Info($"收到商城订单支付回调：OrderNo={orderNo} TransactionId={transactionId} 金额={totalFen}分");
            _orderService.PayOrderByOrderNo(orderNo, PayTypeEnum.Wechat, transactionId, callbackRaw, totalFen);
        }
    }
}
