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
                Channel = "h5",
                AppId = _options.AppId,
                H5Url = h5Response.H5Url
            };
        }

        /// <summary>
        /// 创建微信小程序 JSAPI 预付单，返回吊起 wx.requestPayment 所需的完整参数（含服务端签名的 paySign）。
        /// </summary>
        public async Task<WechatPrepayResult> CreateJSApiPayAsync(string orderNo, string description, decimal amount, string openId)
        {
            if (string.IsNullOrEmpty(openId))
            {
                throw new CustomException("微信小程序支付缺少用户 OpenId");
            }
            var client = BuildClient();
            var appId = string.IsNullOrEmpty(_options.MiniProgramAppId) ? _options.AppId : _options.MiniProgramAppId;
            var request = new CreatePayTransactionJsapiRequest
            {
                AppId = appId,
                OutTradeNumber = orderNo,
                Description = Truncate(description, 120),
                NotifyUrl = _options.NotifyUrl,
                Amount = new CreatePayTransactionJsapiRequest.Types.Amount
                {
                    Total = (int)Math.Round(amount * 100)
                },
                Payer = new CreatePayTransactionJsapiRequest.Types.Payer
                {
                    OpenId = openId
                }
            };

            var flurlReq = client.CreateFlurlRequest(request, System.Net.Http.HttpMethod.Post, new object[] { "pay", "transactions", "jsapi" });
            var jsapiResponse = await client.SendFlurlRequestAsJsonAsync<CreatePayTransactionJsapiResponse>(flurlReq, request, System.Threading.CancellationToken.None);
            if (!string.IsNullOrEmpty(jsapiResponse.ErrorCode))
            {
                throw new CustomException("微信小程序支付下单失败：" + jsapiResponse.ErrorMessage);
            }
            return BuildClientPayParams(orderNo, "miniProgram", appId, null, jsapiResponse.PrepayId, "prepay_id=" + jsapiResponse.PrepayId);
        }

        /// <summary>
        /// 创建 App 原生支付预付单，返回吊起 uni.requestPayment 所需的完整参数（含服务端签名的 paySign）。
        /// </summary>
        public async Task<WechatPrepayResult> CreateAppPayAsync(string orderNo, string description, decimal amount)
        {
            var client = BuildClient();
            var request = new CreatePayTransactionAppRequest
            {
                AppId = _options.AppId,
                OutTradeNumber = orderNo,
                Description = Truncate(description, 120),
                NotifyUrl = _options.NotifyUrl,
                Amount = new CreatePayTransactionAppRequest.Types.Amount
                {
                    Total = (int)Math.Round(amount * 100)
                }
            };

            var flurlReq = client.CreateFlurlRequest(request, System.Net.Http.HttpMethod.Post, new object[] { "pay", "transactions", "app" });
            var appResponse = await client.SendFlurlRequestAsJsonAsync<CreatePayTransactionAppResponse>(flurlReq, request, System.Threading.CancellationToken.None);
            if (!string.IsNullOrEmpty(appResponse.ErrorCode))
            {
                throw new CustomException("微信App支付下单失败：" + appResponse.ErrorMessage);
            }
            // App 端 package 固定为 Sign=WXPay
            return BuildClientPayParams(orderNo, "app", _options.AppId, _options.MerchantId, appResponse.PrepayId, "Sign=WXPay");
        }

        /// <summary>
        /// 组装小程序/App 调起支付所需的参数：时间戳、随机串、package、服务端 RSA 签名的 paySign。
        /// </summary>
        private WechatPrepayResult BuildClientPayParams(string orderNo, string channel, string appId, string partnerId, string prepayId, string package)
        {
            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var nonceStr = Guid.NewGuid().ToString("N");
            var paySign = Sign(appId, timeStamp, nonceStr, package);
            return new WechatPrepayResult
            {
                OrderNo = orderNo,
                Channel = channel,
                AppId = appId,
                PartnerId = partnerId,
                PrepayId = prepayId,
                NonceStr = nonceStr,
                TimeStamp = timeStamp,
                Package = package,
                PaySign = paySign
            };
        }

        /// <summary>
        /// 微信支付 v3 JSAPI/App 调起签名（RSA-SHA256，使用商户 API 私钥）。
        /// 签名串格式：appId\n timestamp\n nonceStr\n package\n
        /// </summary>
        private string Sign(string appId, string timeStamp, string nonceStr, string package)
        {
            if (string.IsNullOrEmpty(_options.MerchantCertificatePrivateKey))
            {
                throw new CustomException("未配置微信支付商户私钥(MerchantCertificatePrivateKey)，无法生成 paySign");
            }
            var message = $"{appId}\n{timeStamp}\n{nonceStr}\n{package}\n";
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportFromPem(_options.MerchantCertificatePrivateKey.ToCharArray());
            var signature = rsa.SignData(System.Text.Encoding.UTF8.GetBytes(message),
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        /// <summary>
        /// 微信小程序：用 wx.login 拿到的 code 换取用户 OpenId（jscode2session）。
        /// 用于游客在小程序内支付时补全 JSAPI 所需的 OpenId。无需额外 NuGet 包。
        /// </summary>
        public async Task<string> GetOpenIdAsync(string code)
        {
            var appId = string.IsNullOrEmpty(_options.MiniProgramAppId) ? _options.AppId : _options.MiniProgramAppId;
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(_options.MiniProgramSecret))
            {
                throw new CustomException("未配置小程序 AppId/MiniProgramSecret，无法换取 OpenId");
            }
            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={_options.MiniProgramSecret}&js_code={code}&grant_type=authorization_code";
            using var http = new System.Net.Http.HttpClient();
            var resp = await http.GetStringAsync(url);
            // 返回示例：{"openid":"...","session_key":"...","unionid":"..."} 或 {"errcode":40029,"errmsg":"..."}
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(resp);
            if (json == null || string.IsNullOrEmpty((string)json.openid))
            {
                var errmsg = json?.errmsg != null ? (string)json.errmsg : "未知错误";
                throw new CustomException("换取 OpenId 失败：" + errmsg);
            }
            return (string)json.openid;
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

                // 安全校验：未支付成功、金额缺失均视为非法回调，拒绝流转订单状态
                if (!string.Equals(resource.TradeState, "SUCCESS", System.StringComparison.OrdinalIgnoreCase))
                {
                    Log.WriteLine(ConsoleColor.Yellow, $"[WechatPay] 回调交易状态非 SUCCESS(={resource.TradeState})，已忽略。OutTradeNo={resource.OutTradeNumber}");
                    return false;
                }
                if (resource.Amount == null || resource.Amount.Total <= 0)
                {
                    Log.WriteLine(ConsoleColor.Red, $"[WechatPay] 回调金额缺失或非法，已拒绝！OutTradeNo={resource.OutTradeNumber}");
                    return false;
                }

                _orderService.PayOrderByOrderNo(resource.OutTradeNumber, PayTypeEnum.Wechat, resource.TransactionId, body, resource.Amount.Total);
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
        /// <summary>H5 跳转地址（仅 h5 通道有值）</summary>
        public string H5Url { get; set; }
        /// <summary>支付通道：h5 / miniProgram / app</summary>
        public string Channel { get; set; }
        /// <summary>小程序/App 的 AppId</summary>
        public string AppId { get; set; }
        /// <summary>App 支付商户号（partnerid）</summary>
        public string PartnerId { get; set; }
        /// <summary>预付单号 prepay_id</summary>
        public string PrepayId { get; set; }
        /// <summary>随机串</summary>
        public string NonceStr { get; set; }
        /// <summary>时间戳（秒）</summary>
        public string TimeStamp { get; set; }
        /// <summary>package 字段：小程序 prepay_id=xxx；App 固定 Sign=WXPay</summary>
        public string Package { get; set; }
        /// <summary>服务端 RSA 签名的 paySign</summary>
        public string PaySign { get; set; }
    }
}
