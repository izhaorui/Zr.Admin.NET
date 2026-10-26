using Microsoft.AspNetCore.Mvc;
using ZR.Model.System.Enums;
using ZR.Payment;

// 创建时间：2026-08-28
namespace ZR.Admin.WebApi.Controllers.System.tenant
{
    /// <summary>
    /// 租户在线续费支付接口。租户管理员在"我的租户"发起微信 H5 支付续费；
    /// 支付结果回调复用商城回调入口（WechatPayGateway 按单号前缀 TO 分发到 TenantRenewalPayHandler）。
    /// 金额由服务端按套餐单价计算，前端仅传时长，不可传金额。
    /// </summary>
    [Route("system/tenant/pay")]
    [ApiExplorerSettings(GroupName = "system")]
    public class TenantPayController : BaseController
    {
        /// <summary>
        /// 续费单价（元/天）。暂用固定价目，后续可挂套餐计价表。
        /// </summary>
        private const decimal DailyPrice = 1m;

        /// <summary>
        /// 单次续费允许的时长范围（天）
        /// </summary>
        private const int MinDays = 30;
        private const int MaxDays = 730;

        private readonly ISysTenantService _sysTenantService;
        private readonly WechatPayGateway _wechatPayGateway;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TenantPayController(
            ISysTenantService sysTenantService,
            WechatPayGateway wechatPayGateway,
            IWebHostEnvironment webHostEnvironment)
        {
            _sysTenantService = sysTenantService;
            _wechatPayGateway = wechatPayGateway;
            _webHostEnvironment = webHostEnvironment;
        }

        /// <summary>
        /// 【已停用】创建微信 H5 在线续费支付单。
        /// 停用原因：公网 C 端与后台拆分后，内网后台宿主在公网不可达，而微信 H5 支付需要
        /// 用户手机可访问的回跳站点（scene.h5_info.app_url 取自 WechatPay:H5ReturnUrl，
        /// 该配置为单值且已归公网商城 H5 使用）。
        /// 租户续费请改用 <see cref="CreateJsapi"/>（系统小程序内支付，参数由服务端直传、无需回跳站点），
        /// 或联系平台线下续费。保留路由是为了给出明确提示，避免前端拿到语义不明的 404。
        /// 如需恢复 H5 通道，请先为租户续费提供公网可达的回跳落地页，见《公网C端与后台拆分部署说明》。
        /// </summary>
        /// <param name="durationDays">续费时长（天），30~730</param>
        [HttpPost("create")]
        //[ActionPermissionFilter(Permission = "tenant:my")]
        [Log(Title = "租户在线续费下单(H5-已停用)", BusinessType = BusinessType.INSERT)]
        public IActionResult Create([FromQuery] int durationDays)
        {
            throw new CustomException("租户续费暂不支持 H5 支付，请在系统小程序内续费，或联系平台线下续费");
        }

        /// <summary>
        /// 创建微信小程序（JSAPI）在线续费支付单，返回 wx.requestPayment 所需的全部参数。
        /// 小程序内无法使用 H5 支付，必须走此通道；OpenId 由 wx.login 的 code 现场换取。
        /// </summary>
        /// <param name="durationDays">续费时长（天），30~730</param>
        /// <param name="code">wx.login 获取的临时登录凭证</param>
        /// <returns></returns>
        [HttpPost("create-jsapi")]
        [ActionPermissionFilter(Permission = "tenant:my")]
        [Log(Title = "租户在线续费下单(小程序)", BusinessType = BusinessType.INSERT)]
        public async Task<IActionResult> CreateJsapi([FromQuery] int durationDays, [FromQuery] string code)
        {
            if (durationDays < MinDays || durationDays > MaxDays)
            {
                throw new CustomException($"续费时长须在{MinDays}~{MaxDays}天之间");
            }
            if (!_wechatPayGateway.Enabled)
            {
                throw new CustomException("在线支付未启用，请联系平台管理员线下续费");
            }
            if (!_wechatPayGateway.IsConfigured)
            {
                throw new CustomException("微信支付已启用但配置不完整（商户号/证书/APIv3 密钥/回调地址需齐全），请联系平台管理员");
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new CustomException("缺少小程序登录凭证 code，无法获取支付 OpenId");
            }

            var tenantId = App.GetCurrentTenantId();
            if (string.IsNullOrWhiteSpace(tenantId) || string.Equals(tenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase))
            {
                throw new CustomException("平台主库租户无需在线续费");
            }
            var tenant = _sysTenantService.GetByTenantId(tenantId);
            if (tenant == null || tenant.DelFlag != DelFlagEnum.Normal)
            {
                throw new CustomException("租户不存在");
            }

            var amount = DailyPrice * durationDays;

            // 与 H5 通道一致：先落 pending 流水再下单，金额/时长/租户以流水为准，回调只认单号
            var order = _sysTenantService.CreateTenantRenewOrder(tenantId, durationDays, amount, HttpContext.GetName());

            var openId = await _wechatPayGateway.GetOpenIdAsync(code);
            var prepay = await _wechatPayGateway.CreateJSApiPayAsync(
                order.OrderNo, $"租户续费{durationDays}天", amount, openId);

            return SUCCESS(new
            {
                orderNo = order.OrderNo,
                amount,
                // 微信支付 v3 JSAPI 调起参数：signType 固定 RSA（服务端用商户私钥签名）
                payParams = new
                {
                    timeStamp = prepay.TimeStamp,
                    nonceStr = prepay.NonceStr,
                    package = prepay.Package,
                    signType = "RSA",
                    paySign = prepay.PaySign
                }
            });
        }

        /// <summary>
        /// 【仅 Development 环境】模拟微信支付成功回调，用于无商户号时验证回调全链路。
        /// 用与真实回调相同的密钥生成签名与加密体，再 HTTP 自调用商城的 wechat/notify 入口，
        /// 因此验签、解密、分发、金额比对、幂等、续费、置付等逻辑与线上完全一致。
        /// </summary>
        /// <param name="orderNo">待支付的续费流水单号</param>
        /// <param name="totalFen">模拟支付金额（分）；不传则取流水应付金额，传错值用于验证金额不符被拒</param>
        /// <returns></returns>
        [HttpPost("mock-notify")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionPermissionFilter(Permission = "tenant:my")]
        public async Task<IActionResult> MockNotify(string orderNo, int? totalFen = null)
        {
            if (!_webHostEnvironment.IsDevelopment())
            {
                return ToResponse(ApiResult.Error("模拟回调仅允许在 Development 环境使用"));
            }

            var order = _sysTenantService.GetTenantOrderByNo(orderNo);
            if (order == null)
            {
                throw new CustomException("支付单不存在");
            }
            // 与 WechatPayGateway.ToFen / 回调金额校验保持一致：统一 AwayFromZero，避免差 1 分
            var amountFen = totalFen ?? (int)Math.Round((order.Amount ?? 0m) * 100, MidpointRounding.AwayFromZero);

            var mock = _wechatPayGateway.BuildMockNotify(order.OrderNo, "MOCK" + Guid.NewGuid().ToString("N")[..12], amountFen);

            // 自调用真实回调入口，确保走完整验签解密分发链路。
            // 拆分后回调入口在公网 C 端宿主上，内网宿主的 Host 已无该路由，
            // 因此优先读 WechatPay:MockNotifyUrl，其次 WechatPay:NotifyUrl，最后才回退本机地址。
            var notifyUrl = App.Configuration["WechatPay:MockNotifyUrl"];
            if (string.IsNullOrWhiteSpace(notifyUrl))
            {
                notifyUrl = App.Configuration["WechatPay:NotifyUrl"];
            }
            if (string.IsNullOrWhiteSpace(notifyUrl))
            {
                notifyUrl = $"{Request.Scheme}://{Request.Host}/shopping/front/order/wechat/notify";
            }
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Wechatpay-Timestamp", mock.Timestamp);
            http.DefaultRequestHeaders.Add("Wechatpay-Nonce", mock.Nonce);
            http.DefaultRequestHeaders.Add("Wechatpay-Signature", mock.Signature);
            http.DefaultRequestHeaders.Add("Wechatpay-Serial", mock.Serial);
            var content = new StringContent(mock.Body, global::System.Text.Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(notifyUrl, content);
            var respBody = await resp.Content.ReadAsStringAsync();

            return SUCCESS(new { notifyUrl, statusCode = (int)resp.StatusCode, response = respBody });
        }

        /// <summary>
        /// 查询续费支付状态（前端跳转微信支付后轮询用）。已支付返回 paid=true。
        /// </summary>
        /// <param name="orderNo">下单接口返回的流水单号</param>
        /// <returns></returns>
        [HttpGet("status")]
        [ActionPermissionFilter(Permission = "tenant:my")]
        public IActionResult Status(string orderNo)
        {
            var order = _sysTenantService.GetTenantOrderByNo(orderNo);
            if (order == null)
            {
                throw new CustomException("支付单不存在");
            }
            return SUCCESS(new
            {
                orderNo = order.OrderNo,
                paid = order.PayStatus == 1,
                amount = order.Amount,
                payTime = order.PayTime
            });
        }
    }
}
