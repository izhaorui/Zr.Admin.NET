using Infrastructure;
using Infrastructure.Attribute;
using SKIT.FlurlHttpClient.Wechat.TenpayV3;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Events;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Models;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Settings;
using ZR.Common;
using ZR.Mall.Enum;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Payment
{
    /// <summary>
    /// 微信支付 V3（H5）封装。平台统一商户号模式。
    /// - 发起支付：创建 H5 预付单，返回 h5_url 供前端吊起。
    /// - 异步回调：验签 + 解密，成功后调用订单服务的"按订单号支付"。
    /// 状态机与库存逻辑全部在 OMSOrderService，本服务只负责支付渠道对接。
    /// 配置来源：appsettings.json 的 WechatPay 节点（AppSettings.Get 绑定）。
    /// 基于 SKIT.FlurlHttpClient.Wechat.TenpayV3 3.16.0。
    /// </summary>
    [AppService(ServiceType = typeof(WechatPayService))]
    public class WechatPayService
    {
        private readonly WechatPayOptions _options;
        private readonly IOMSOrderService _orderService;

        public WechatPayService(IOMSOrderService orderService)
        {
            _options = AppSettings.Get<WechatPayOptions>("WechatPay") ?? new WechatPayOptions();
            _orderService = orderService;
        }

        public bool Enabled => _options.Enabled;

        private WechatTenpayClient BuildClient()
        {
            var clientOptions = new WechatTenpayClientOptions
            {
                MerchantId = _options.MerchantId,
                MerchantCertificateSerialNumber = _options.MerchantCertificateSerialNumber,
                MerchantCertificatePrivateKey = _options.MerchantCertificatePrivateKey,
                MerchantV3Secret = _options.MerchantV3Key
            };

            if (!string.IsNullOrEmpty(_options.WechatPayPublicKeyId) && !string.IsNullOrEmpty(_options.WechatPayPublicKey))
            {
                var publicKeyManager = new InMemoryPublicKeyManager();
                publicKeyManager.AddEntry(new PublicKeyEntry(
                    PublicKeyEntry.ALGORITHM_TYPE_RSA,
                    _options.WechatPayPublicKeyId,
                    _options.WechatPayPublicKey));
                clientOptions.PlatformAuthScheme = PlatformAuthScheme.PublicKey;
                clientOptions.PlatformPublicKeyManager = publicKeyManager;
            }

            return new WechatTenpayClient(clientOptions);
        }

        /// <summary>
        /// 创建 H5 预付单，返回 h5_url（mweb_url）供前端吊起微信支付。
        /// </summary>
        public async Task<WechatPrepayResult> CreateH5PayAsync(string orderNo, string description, decimal amount, string clientIp)
        {
            var client = BuildClient();
            var request = new CreatePayTransactionH5Request
            {
                AppId = _options.AppId,
                OutTradeNumber = orderNo,
                Description = Truncate(description, 120),
                NotifyUrl = _options.NotifyUrl,
                Amount = new CreatePayTransactionH5Request.Types.Amount
                {
                    Total = (int)Math.Round(amount * 100)
                },
                Scene = new CreatePayTransactionH5Request.Types.Scene
                {
                    H5Info = new CreatePayTransactionH5Request.Types.Scene.Types.H5Info
                    {
                        Type = "Wap",
                        AppUrl = _options.H5ReturnUrl
                    }
                }
            };

            // v3.16.0 底层调用：CreateFlurlRequest + SendFlurlRequestAsJsonAsync
            var flurlReq = client.CreateFlurlRequest(request, System.Net.Http.HttpMethod.Post, new object[] { "pay", "transactions", "h5" });
            var h5Response = await client.SendFlurlRequestAsJsonAsync<CreatePayTransactionH5Response>(flurlReq, request, System.Threading.CancellationToken.None);
            if (!string.IsNullOrEmpty(h5Response.ErrorCode))
            {
                throw new CustomException("微信支付下单失败：" + h5Response.ErrorMessage);
            }
            return new WechatPrepayResult
            {
                OrderNo = orderNo,
                H5Url = h5Response.H5Url
            };
        }

        /// <summary>
        /// 处理微信支付异步回调。返回 true 表示验签+解密成功并已完成订单状态流转。
        /// </summary>
        public bool HandleNotify(string timestamp, string nonce, string signature, string serial, string body)
        {
            var client = BuildClient();
            if (!client.VerifyEventSignature(
                webhookTimestamp: timestamp,
                webhookNonce: nonce,
                webhookBody: body,
                webhookSignature: signature,
                webhookSerialNumber: serial))
            {
                return false;
            }

            var callbackModel = client.DeserializeEvent(body);
            if (string.Equals(callbackModel.EventType, "TRANSACTION.SUCCESS", System.StringComparison.OrdinalIgnoreCase))
            {
                var resource = client.DecryptEventResource<TransactionResource>(callbackModel);
                _orderService.PayOrderByOrderNo(resource.OutTradeNumber, PayTypeEnum.Wechat, resource.TransactionId, body);
            }
            return true;
        }

        private static string Truncate(string s, int maxByte)
        {
            if (string.IsNullOrEmpty(s)) return "商城订单";
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            if (bytes.Length <= maxByte) return s;
            return System.Text.Encoding.UTF8.GetString(bytes, 0, maxByte) + "...";
        }
    }

    public class WechatPrepayResult
    {
        public string OrderNo { get; set; }
        public string H5Url { get; set; }
    }
}
