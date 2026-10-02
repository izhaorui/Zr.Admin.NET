using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.ServiceCore.Payment;

//创建时间：2026-07-25
namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// 商城 C 端「支付回调 + 微信支付工具」接口（匿名可访问）。
    ///
    /// 保留两个端点，它们都不是"游客功能"：
    /// - wechat/notify：微信支付异步回调。**必须匿名**（微信服务器调用），且**路由不可改**（商户平台已配置该地址）；
    /// - wx/openid：小程序 code 换 OpenId，支付前置工具，与用户身份无关。
    /// </summary>
    [PublicApi]
    [Route("shopping/front/order")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontOrderController : BaseController
    {
        /// <summary>技术日志（NLog → logs/all.log）</summary>
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly WechatPayGateway _wechatPayGateway;

        public FrontOrderController(WechatPayGateway wechatPayGateway)
        {
            _wechatPayGateway = wechatPayGateway;
        }

        /// <summary>
        /// 微信小程序：用 wx.login 返回的 code 换取用户 OpenId（小程序支付补全 JSAPI 参数）。
        /// 与用户身份无关（拿的是调用者自己微信的 OpenId），故保持匿名；登录与否都可调用。
        /// </summary>
        [HttpGet("wx/openid")]
        public async Task<IActionResult> GetWxOpenId([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "code 不能为空");
            }
            var openId = await _wechatPayGateway.GetOpenIdAsync(code);
            return SUCCESS(new { openId });
        }

        /// <summary>
        /// 微信支付结果异步回调（微信服务器调用，验签+解密后按订单号完成支付）。
        /// 按微信规范：接收成功返回 200 {code:SUCCESS}；验签失败返回 400 FAIL 触发微信重推。
        /// <para>
        /// ⚠️ 本路由已配置在微信商户平台，**禁止修改**：改了会导致回调打不到、订单永远停在"待付款"。
        /// </para>
        /// </summary>
        [HttpPost("wechat/notify")]
        public async Task<IActionResult> WechatPayNotify()
        {
            string timestamp = Request.Headers["Wechatpay-Timestamp"];
            string nonce = Request.Headers["Wechatpay-Nonce"];
            string signature = Request.Headers["Wechatpay-Signature"];
            string serial = Request.Headers["Wechatpay-Serial"];
            string body;
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }
            try
            {
                var ok = _wechatPayGateway.HandleNotify(timestamp, nonce, signature, serial, body);
                if (ok)
                {
                    return new JsonResult(new { code = "SUCCESS", message = "成功" });
                }
                // 失败原因（未配置/验签失败/金额非法/无 handler）已在网关内记 NLog，这里只回失败应答
                return StatusCode(400, new { code = "FAIL", message = "验签失败" });
            }
            catch (Exception ex)
            {
                // 业务异常（如订单不存在）也返回 FAIL 让微信重推；异常详情只进日志，不回给调用方
                logger.Error(ex, $"[微信支付回调] 处理失败，已返回 FAIL 触发微信重推。Serial={serial} BodyLength={body?.Length ?? 0}");
                return StatusCode(500, new { code = "FAIL", message = "回调处理失败" });
            }
        }
    }
}
