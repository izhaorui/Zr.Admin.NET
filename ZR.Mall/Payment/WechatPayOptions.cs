namespace ZR.Mall.Payment
{
    /// <summary>
    /// 微信支付 V3 配置（平台统一商户号，非多租户商户配置）。
    /// 配置来源：appsettings.json 的 WechatPay 节点。
    /// 生产环境密钥/证书应通过密钥管理（如 KMS / UserSecrets / 配置中心）注入，勿明文提交。
    /// </summary>
    public class WechatPayOptions
    {
        /// <summary>
        /// 是否启用真实微信支付；false 时下单支付走原模拟流程（开发/联调用）
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 微信支付商户号（直连商户 mchid）
        /// </summary>
        public string MerchantId { get; set; }

        /// <summary>
        /// 公众号/移动应用 AppId（H5 支付使用绑定的 AppId）
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// 商户 API 证书序列号
        /// </summary>
        public string MerchantCertificateSerialNumber { get; set; }

        /// <summary>
        /// 商户 API 私钥（PEM，含 -----BEGIN PRIVATE KEY-----）
        /// </summary>
        public string MerchantCertificatePrivateKey { get; set; }

        /// <summary>
        /// APIv3 密钥（用于回调报文解密）
        /// </summary>
        public string MerchantV3Key { get; set; }

        /// <summary>
        /// 微信支付公钥 ID（2024-10 后平台公钥验签模式；替代平台证书下载）
        /// </summary>
        public string WechatPayPublicKeyId { get; set; }

        /// <summary>
        /// 微信支付公钥（PEM，用于验签回调通知）
        /// </summary>
        public string WechatPayPublicKey { get; set; }

        /// <summary>
        /// 支付结果异步回调地址（公网 HTTPS，需在商户平台配置）
        /// </summary>
        public string NotifyUrl { get; set; }

        /// <summary>
        /// H5 支付场景的 Wap 站点地址（回跳/展示用）
        /// </summary>
        public string H5ReturnUrl { get; set; }
    }
}
