using Infrastructure;
using Infrastructure.Attribute;
using SKIT.FlurlHttpClient.Wechat.TenpayV3;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Events;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Models;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Settings;

namespace ZR.ServiceCore.Payment
{
    /// <summary>
    /// 微信支付 V3 渠道网关（平台统一商户号模式）。只负责支付渠道对接，不含任何业务订单逻辑：
    /// - 下单：H5 / JSAPI / App 三通道，返回各端吊起支付所需参数；
    /// - 回调：验签 + 解密，按商户单号分发给 IWechatPayNotifyHandler 实现（商城订单/租户续费等各自注册）。
    /// 配置来源：appsettings.json 的 WechatPay 节点。基于 SKIT.FlurlHttpClient.Wechat.TenpayV3 3.16.0。
    /// 注意：构造注入了 Scoped 的回调 handler 集合，故本服务必须为 Scoped（不可 Singleton）。
    /// </summary>
    [AppService(ServiceType = typeof(WechatPayGateway), ServiceLifetime = LifeTime.Scoped)]
    public class WechatPayGateway
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly WechatPayOptions _options;
        private readonly IEnumerable<IWechatPayNotifyHandler> _notifyHandlers;

        public WechatPayGateway(IEnumerable<IWechatPayNotifyHandler> notifyHandlers)
        {
            _options = AppSettings.Get<WechatPayOptions>("WechatPay") ?? new WechatPayOptions();
            _notifyHandlers = notifyHandlers ?? Enumerable.Empty<IWechatPayNotifyHandler>();
        }

        public bool Enabled => _options.Enabled;

        /// <summary>
        /// 真实支付所需的关键配置是否齐全（商户号 / 证书序列号 / 商户私钥 / APIv3 密钥 / 回调地址）。
        ///
        /// 只看 <see cref="Enabled"/> 是不够的：开关打开但配置留空时，客户端构造与建预付单会在 SDK 内部
        /// 抛"参数不能为空"之类的异常，调用方拿不到可读原因。支付入口应先判断本属性再给出明确提示。
        ///
        /// 与 <c>IOssProvider.IsConfigured</c> 同一约定：配置不齐时不抛异常，由入口负责提示。
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_options.MerchantId)
            && !string.IsNullOrWhiteSpace(_options.MerchantCertificateSerialNumber)
            && !string.IsNullOrWhiteSpace(_options.MerchantCertificatePrivateKey)
            && !string.IsNullOrWhiteSpace(_options.MerchantV3Key)
            && !string.IsNullOrWhiteSpace(_options.NotifyUrl);

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
                    Total = ToFen(amount)
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
            var h5Response = await SendRequestAsync<CreatePayTransactionH5Response>(client, flurlReq, request, "微信H5支付下单");
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
                    Total = ToFen(amount)
                },
                Payer = new CreatePayTransactionJsapiRequest.Types.Payer
                {
                    OpenId = openId
                }
            };

            var flurlReq = client.CreateFlurlRequest(request, System.Net.Http.HttpMethod.Post, new object[] { "pay", "transactions", "jsapi" });
            var jsapiResponse = await SendRequestAsync<CreatePayTransactionJsapiResponse>(client, flurlReq, request, "微信小程序支付下单");
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
                    Total = ToFen(amount)
                }
            };

            var flurlReq = client.CreateFlurlRequest(request, System.Net.Http.HttpMethod.Post, new object[] { "pay", "transactions", "app" });
            var appResponse = await SendRequestAsync<CreatePayTransactionAppResponse>(client, flurlReq, request, "微信App支付下单");
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
        /// 微信小程序：用 wx.login 拿到的 code 换取用户 OpenId（jscode2session）。无需额外 NuGet 包。
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
        /// 处理微信支付异步回调：验签 + 解密，成功交易按商户单号分发给注册的 IWechatPayNotifyHandler。
        /// 返回 true 表示验签成功且分发完成（业务处理失败会抛异常向上传递，由回调端记日志）。
        /// 失败原因统一走 NLog（落 logs/all.log），便于线上排查"回调丢单/无 handler/金额非法"。
        /// </summary>
        public bool HandleNotify(string timestamp, string nonce, string signature, string serial, string body)
        {
            // 配置不齐时无法验签/解密：一律拒绝（绝不能放行未验证的回调），并记 Error 让运维发现"回调地址已配但参数缺失"
            if (!IsConfigured)
            {
                logger.Error($"[微信支付] 收到回调但 WechatPay 配置不完整（商户号/证书序列号/商户私钥/APIv3密钥/回调地址），已拒绝。Serial={serial}");
                return false;
            }

            var client = BuildClient();
            if (!client.VerifyEventSignature(
                webhookTimestamp: timestamp,
                webhookNonce: nonce,
                webhookBody: body,
                webhookSignature: signature,
                webhookSerialNumber: serial))
            {
                logger.Warn($"[微信支付] 回调验签失败，已拒绝。Serial={serial}");
                return false;
            }

            var callbackModel = client.DeserializeEvent(body);
            if (string.Equals(callbackModel.EventType, "TRANSACTION.SUCCESS", System.StringComparison.OrdinalIgnoreCase))
            {
                var resource = client.DecryptEventResource<TransactionResource>(callbackModel);

                // 安全校验：未支付成功、金额缺失均视为非法回调，直接拒绝
                if (!string.Equals(resource.TradeState, "SUCCESS", System.StringComparison.OrdinalIgnoreCase))
                {
                    logger.Warn($"[微信支付] 回调交易状态非 SUCCESS(={resource.TradeState})，已忽略。OutTradeNo={resource.OutTradeNumber}");
                    return false;
                }
                if (resource.Amount == null || resource.Amount.Total <= 0)
                {
                    logger.Error($"[微信支付] 回调金额缺失或非法，已拒绝！OutTradeNo={resource.OutTradeNumber}");
                    return false;
                }

                var handler = _notifyHandlers.FirstOrDefault(h => h.CanHandle(resource.OutTradeNumber));
                if (handler == null)
                {
                    logger.Error($"[微信支付] 无 handler 认领商户单号 {resource.OutTradeNumber}，请检查 IWechatPayNotifyHandler 注册");
                    return false;
                }

                // 解密后的资源报文原样传给业务方落库（oms_payment.CallbackRaw），供对账/排查
                var callbackRaw = Newtonsoft.Json.JsonConvert.SerializeObject(resource);
                logger.Info($"[微信支付] 回调分发开始：OutTradeNo={resource.OutTradeNumber} TransactionId={resource.TransactionId} 金额={resource.Amount.Total}分 Handler={handler.GetType().Name}");
                handler.HandlePaid(resource.OutTradeNumber, resource.TransactionId, resource.Amount.Total, callbackRaw);
                logger.Info($"[微信支付] 回调分发完成：OutTradeNo={resource.OutTradeNumber} TransactionId={resource.TransactionId}");
            }
            return true;
        }

        /// <summary>
        /// 统一执行微信支付接口请求：业务错误码转业务异常，底层异常（签名失败/证书不匹配/网络等）
        /// 附加内层原因后抛出，便于定位配置问题（如私钥格式错误、证书序列号不匹配）。
        /// </summary>
        private async Task<TResponse> SendRequestAsync<TResponse>(WechatTenpayClient client, Flurl.Http.IFlurlRequest flurlReq, object data, string actionName)
            where TResponse : WechatTenpayResponse, new()
        {
            try
            {
                var response = await client.SendFlurlRequestAsJsonAsync<TResponse>(flurlReq, data, System.Threading.CancellationToken.None);
                if (!string.IsNullOrEmpty(response.ErrorCode))
                {
                    throw new CustomException($"{actionName}失败：{response.ErrorCode} {response.ErrorMessage}");
                }
                return response;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var detail = ex.InnerException == null
                    ? ex.Message
                    : $"{ex.Message}（内部原因：{ex.InnerException.Message}）";
                throw new CustomException($"{actionName}失败，请检查 WechatPay 配置（商户号/证书序列号/商户私钥 PEM 格式）：{detail}");
            }
        }

        /// <summary>
        /// 构造一笔模拟的微信支付成功回调（仅本地联调用，无商户号时验证回调全链路）。
        /// 使用与真实回调完全相同的密钥配置（APIv3Key 加密 + 配置中的公钥验签模式）生成事件体与签名头，
        /// 因此后续 HandleNotify 的验签、解密、分发、金额比对、幂等、续费等逻辑与线上完全一致，
        /// 仅"微信服务器"是本地模拟。
        /// </summary>
        /// <param name="orderNo">商户单号</param>
        /// <param name="transactionId">模拟的交易号</param>
        /// <param name="totalFen">支付金额（分）</param>
        /// <returns>事件体与四个回调头</returns>
        public WechatMockNotify BuildMockNotify(string orderNo, string transactionId, int totalFen)
        {
            if (string.IsNullOrWhiteSpace(_options.MerchantV3Key))
            {
                throw new CustomException("未配置 WechatPay:MerchantV3Key，无法生成模拟回调");
            }
            if (string.IsNullOrWhiteSpace(_options.MerchantCertificatePrivateKey))
            {
                throw new CustomException("未配置 WechatPay:MerchantCertificatePrivateKey，无法生成模拟回调签名");
            }

            var resource = new
            {
                appid = string.IsNullOrEmpty(_options.MiniProgramAppId) ? _options.AppId : _options.MiniProgramAppId,
                mchid = _options.MerchantId,
                out_trade_no = orderNo,
                transaction_id = transactionId,
                trade_type = "MWEB",
                trade_state = "SUCCESS",
                trade_state_desc = "支付成功",
                bank_type = "OTHERS",
                attach = "",
                success_time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                payer = new { openid = "mock_openid" },
                amount = new { total = totalFen, currency = "CNY", payer_total = totalFen, payer_currency = "CNY" }
            };

            var body = new
            {
                id = Guid.NewGuid().ToString("N"),
                create_time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                resource_type = "encrypt-resource",
                event_type = "TRANSACTION.SUCCESS",
                summary = "支付成功",
                resource = new
                {
                    algorithm = "AEAD_AES_256_GCM",
                    ciphertext = "",
                    associated_data = "transaction",
                    nonce = Guid.NewGuid().ToString("N")[..12]
                }
            };

            // AES-GCM 加密 resource.ciphertext（APIv3 规范：ciphertext = Base64(AES-GCM(明文, key, nonce, aad))）
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(resource));
            var cipherBytes = new byte[plainBytes.Length + 16];
            var tag = new byte[16];
            var nonceBytes = System.Text.Encoding.UTF8.GetBytes(body.resource.nonce);
            var aadBytes = System.Text.Encoding.UTF8.GetBytes(body.resource.associated_data);
            using (var gcm = new System.Security.Cryptography.AesGcm(System.Text.Encoding.UTF8.GetBytes(_options.MerchantV3Key), 16))
            {
                gcm.Encrypt(nonceBytes, plainBytes, cipherBytes.AsSpan(0, plainBytes.Length), tag, aadBytes);
            }
            var cipherWithTag = new byte[plainBytes.Length + 16];
            Buffer.BlockCopy(cipherBytes, 0, cipherWithTag, 0, plainBytes.Length);
            Buffer.BlockCopy(tag, 0, cipherWithTag, plainBytes.Length, 16);

            var finalBody = new
            {
                body.id,
                body.create_time,
                body.resource_type,
                body.event_type,
                body.summary,
                resource = new
                {
                    body.resource.algorithm,
                    ciphertext = Convert.ToBase64String(cipherWithTag),
                    body.resource.associated_data,
                    body.resource.nonce
                }
            };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(finalBody);

            // 回调签名：timestamp\n nonce\n body\n（使用商户 API 私钥）
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var nonce = Guid.NewGuid().ToString("N");
            var message = $"{timestamp}\n{nonce}\n{json}\n";
            string signature;
            using (var rsa = System.Security.Cryptography.RSA.Create())
            {
                rsa.ImportFromPem(_options.MerchantCertificatePrivateKey.ToCharArray());
                signature = Convert.ToBase64String(rsa.SignData(System.Text.Encoding.UTF8.GetBytes(message),
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    System.Security.Cryptography.RSASignaturePadding.Pkcs1));
            }

            return new WechatMockNotify
            {
                Body = json,
                Timestamp = timestamp,
                Nonce = nonce,
                Signature = signature,
                Serial = _options.MerchantCertificateSerialNumber
            };
        }

        /// <summary>
        /// 元 → 分。统一使用 AwayFromZero，与回调侧的金额校验
        /// （OMSOrderStateService.PayOrderByOrderNo 的 expectedFen）保持一致 ——
        /// 两处若一个用默认的银行家舍入、一个用 AwayFromZero，极端金额下会差 1 分，
        /// 导致真实回调被误判为"金额不一致"而拒绝。
        /// </summary>
        private static int ToFen(decimal amount) => (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

        private static string Truncate(string s, int maxByte)
        {
            if (string.IsNullOrEmpty(s)) return "商城订单";
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            if (bytes.Length <= maxByte) return s;
            return System.Text.Encoding.UTF8.GetString(bytes, 0, maxByte) + "...";
        }
    }

    /// <summary>
    /// 模拟微信支付回调的请求内容（本地联调用）
    /// </summary>
    public class WechatMockNotify
    {
        /// <summary>事件体 JSON</summary>
        public string Body { get; set; }

        /// <summary>Wechatpay-Timestamp 头</summary>
        public string Timestamp { get; set; }

        /// <summary>Wechatpay-Nonce 头</summary>
        public string Nonce { get; set; }

        /// <summary>Wechatpay-Signature 头</summary>
        public string Signature { get; set; }

        /// <summary>Wechatpay-Serial 头</summary>
        public string Serial { get; set; }
    }

    public class WechatPrepayResult
    {
        public string OrderNo { get; set; }
        /// <summary>H5 跳转地址（仅 h5 通道有值）</summary>
        public string H5Url { get; set; }
        /// <summary>支付通道：h5 / miniProgram / app</summary>
        public string Channel { get; set; }
        public string AppId { get; set; }
        /// <summary>App 支付商户号（partnerid）</summary>
        public string PartnerId { get; set; }
        /// <summary>预付单号 prepay_id</summary>
        public string PrepayId { get; set; }
        public string NonceStr { get; set; }
        public string TimeStamp { get; set; }
        /// <summary>package 字段：小程序 prepay_id=xxx；App 固定 Sign=WXPay</summary>
        public string Package { get; set; }
        /// <summary>服务端 RSA 签名的 paySign</summary>
        public string PaySign { get; set; }
    }
}
