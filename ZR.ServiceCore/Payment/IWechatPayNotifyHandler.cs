namespace ZR.ServiceCore.Payment
{
    /// <summary>
    /// 微信支付回调业务分发接口。WechatPayGateway 完成验签解密后，按商户单号遍历已注册的
    /// handler 分发成功交易；各业务模块（商城订单/租户续费等）各自实现，互不依赖。
    /// </summary>
    public interface IWechatPayNotifyHandler
    {
        /// <summary>
        /// 是否由本 handler 处理该商户单号（按单号前缀等规则路由）
        /// </summary>
        bool CanHandle(string outTradeNo);

        /// <summary>
        /// 处理支付成功的交易。实现方自行完成幂等与金额校验，失败时抛异常（回调端统一记日志并返回失败应答）。
        /// </summary>
        /// <param name="orderNo">商户单号 OutTradeNumber</param>
        /// <param name="transactionId">微信支付订单号</param>
        /// <param name="totalFen">实际支付金额（分）</param>
        /// <param name="callbackRaw">
        /// 回调资源报文（解密后的 JSON），供落库对账/排查；网关无法解密时为 null。
        /// </param>
        void HandlePaid(string orderNo, string transactionId, int totalFen, string callbackRaw = null);
    }
}
