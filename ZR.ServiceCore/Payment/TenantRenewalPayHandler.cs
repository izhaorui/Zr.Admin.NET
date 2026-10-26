using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Payment;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Payment
{
    /// <summary>
    /// 租户在线续费微信支付回调处理器：认领 TO 前缀（SysTenantOrder 流水单号）的支付成功回调。
    /// 金额校验（回调分 vs 流水 Amount）→ 幂等（已支付直接放行）→ 调 RenewTenant 执行续费
    /// （内部自动同步套餐绑定 EndTime 并写入 renew 计费流水）→ 流水置已支付。
    /// </summary>
    [AppService(ServiceType = typeof(IWechatPayNotifyHandler), ServiceLifetime = LifeTime.Scoped)]
    public class TenantRenewalPayHandler : IWechatPayNotifyHandler
    {
        private static NLog.Logger Logger => NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysTenantService _tenantService;

        public TenantRenewalPayHandler(ISysTenantService tenantService)
        {
            _tenantService = tenantService;
        }

        public bool CanHandle(string outTradeNo)
        {
            return !string.IsNullOrWhiteSpace(outTradeNo)
                && outTradeNo.StartsWith("TO", StringComparison.OrdinalIgnoreCase);
        }

        public void HandlePaid(string orderNo, string transactionId, int totalFen, string callbackRaw = null)
        {
            var order = _tenantService.GetTenantOrderByNo(orderNo);
            if (order == null)
            {
                Logger.Info($"[TenantRenewalPayHandler] 支付回调找不到续费流水 {orderNo}，请人工核对微信账单");
                throw new CustomException($"续费流水{orderNo}不存在");
            }

            // 幂等：已支付直接放行（微信可能重推回调）
            if (order.PayStatus == 1) return;

            // 金额校验：回调金额(分)必须与流水应付金额一致，防篡改/串单
            if (order.Amount == null || (int)Math.Round(order.Amount.Value * 100) != totalFen)
            {
                Logger.Info($"[TenantRenewalPayHandler] 续费流水 {orderNo} 金额不符：应付 {order.Amount}，实付 {totalFen} 分，已拒绝入账");
                throw new CustomException("回调金额与流水不符");
            }

            var tenant = _tenantService.GetByTenantId(order.TenantId);
            if (tenant == null)
            {
                Logger.Info($"[TenantRenewalPayHandler] 续费流水 {orderNo} 对应租户[{order.TenantId}]不存在");
                throw new CustomException("租户不存在");
            }

            var extendDays = order.DurationDays ?? 365;
            _tenantService.RenewTenant(new TenantRenewDto
            {
                TenantId = order.TenantId,
                ExtendDays = extendDays,
                Amount = order.Amount,
                Remark = $"微信支付在线续费（单号{orderNo}）"
            }, "wxpay");

            _tenantService.MarkTenantOrderPaid(orderNo, transactionId);
            Logger.Info($"[TenantRenewalPayHandler] 租户[{order.TenantId}]在线续费完成：{orderNo}，续费天数 {extendDays}");
        }
    }
}
