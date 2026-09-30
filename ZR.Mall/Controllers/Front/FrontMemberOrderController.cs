using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using ZR.Mall.Common;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Payment;

namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// C 端会员订单接口（下单 / 支付 / 我的订单 / 取消）。
    ///
    /// 会员身份一律取自令牌（请求头 mall-token），不接受手机号作为归属凭据；
    /// 查询与归属范围只按 MemberId（见 <see cref="OrderOwnership"/>）。
    /// </summary>
    [PublicApi]
    [Route("front/member/order")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontMemberOrderController : BaseController
    {
        private readonly IFrontOrderService _orderService;
        private readonly WechatPayGateway _wechatPayGateway;
        private readonly IOMSPaymentService _paymentService;

        /// <summary>技术日志（NLog → logs/all.log）</summary>
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public FrontMemberOrderController(IFrontOrderService orderService, WechatPayGateway wechatPayGateway, IOMSPaymentService paymentService)
        {
            _orderService = orderService;
            _wechatPayGateway = wechatPayGateway;
            _paymentService = paymentService;
        }

        /// <summary>
        /// 下单收货手机号仅为联系信息，不参与归属判定；
        /// 会员地址簿里存家人手机号是常态，故不再要求任何短信验证码。
        /// </summary>
        [HttpPost("create")]
        [MemberAuthorize]
        public IActionResult Create([FromBody] CreateOrderDto dto)
        {
            if (dto?.Items == null || dto.Items.Count == 0)
            {
                return ToResponse(ResultCode.PARAM_ERROR, "下单商品不能为空");
            }
            if (dto.Address == null || string.IsNullOrWhiteSpace(dto.Address.Phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "收货手机号不能为空");
            }
            if (!Regex.IsMatch(dto.Address.Phone, @"^1\d{10}$"))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号格式不正确");
            }

            // 身份取自令牌（MemberAuthorize 已校验通过）
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);

            // 下单防刷/限流（IP + 手机号维度）
            var ip = HttpContextExtension.GetClientUserIp(HttpContext);
            if (!_orderService.TryAcquireCreateQuota(ip, dto.Address.Phone, out var quotaMsg))
            {
                return ToResponse(ResultCode.FAIL, quotaMsg);
            }

            var order = _orderService.CreateOrder(dto, member.MemberId);
            return SUCCESS(new { order.OrderNo, order.Id, order.TotalAmount, order.OrderStatus });
        }

        /// <summary>
        /// 发起支付（需登录）。
        /// 鉴权：一律按会员身份（令牌），手机号不再是支付凭据。
        /// - WechatPay:Enabled=false：模拟支付（开发/联调），直接 待付款 → 待发货；
        /// - WechatPay:Enabled=true：创建微信预付单。H5 返回 h5Url；小程序/App 返回 payParams，
        ///   状态流转在微信异步回调（shopping/front/order/wechat/notify）完成。
        /// </summary>
        [HttpPost("pay")]
        [MemberAuthorize]
        public async Task<IActionResult> Pay([FromBody] FrontPayOrderDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.OrderNo))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "订单号不能为空");
            }
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);

            if (!_wechatPayGateway.Enabled)
            {
                // 模拟支付通道（开发环境）
                var mockOrder = _orderService.PayOrder(dto.OrderNo, member.MemberId);
                return SUCCESS(new { mockOrder.OrderNo, mockOrder.OrderStatus, mockOrder.PayType, mockOrder.PayTime, mockOrder.PayAmount });
            }

            // 开关已打开但关键配置留空：建预付单会在 SDK 内部抛异常，错误信息对用户无意义，提前提示。
            // ⚠️ 这里不能"降级成模拟支付" —— 那等于把没收到钱的订单标记为已收款。
            if (!_wechatPayGateway.IsConfigured)
            {
                logger.Error($"微信支付已启用但配置不完整，已拒绝发起支付：OrderNo={dto.OrderNo} MemberId={member.MemberId}");
                return ToResponse(ResultCode.FAIL, "微信支付未配置完整，请联系管理员补全支付配置");
            }

            // 真实支付：校验归属与状态后按通道创建微信预付单
            var order = _orderService.Queryable()
                .Includes(x => x.Items)
                .Where(x => x.OrderNo == dto.OrderNo && x.IsDelete == 0)
                .First();
            if (!OrderOwnership.IsOwnedBy(order, member.MemberId))
            {
                return ToResponse(ResultCode.FAIL, "订单不存在或无权限");
            }
            if (order.OrderStatus != ZR.Mall.Enum.OrderStatusEnum.None)
            {
                return ToResponse(ResultCode.FAIL, "订单当前状态不可支付");
            }
            var desc = order.Items?.FirstOrDefault()?.ProductName ?? "商城订单";
            if (order.Items?.Count > 1) desc += $" 等{order.Items.Count}件商品";
            var ip = HttpContextExtension.GetClientUserIp(HttpContext);

            // 按通道选择预付单类型
            var channel = string.IsNullOrEmpty(dto.Channel) ? "h5" : dto.Channel.ToLowerInvariant();
            WechatPrepayResult prepay;
            if (channel == "miniprogram")
            {
                prepay = await _wechatPayGateway.CreateJSApiPayAsync(order.OrderNo, desc, order.PayAmount, dto.OpenId);
            }
            else if (channel == "app")
            {
                prepay = await _wechatPayGateway.CreateAppPayAsync(order.OrderNo, desc, order.PayAmount);
            }
            else
            {
                prepay = await _wechatPayGateway.CreateH5PayAsync(order.OrderNo, desc, order.PayAmount, ip);
            }

            // 记录预支付流水（状态=Prepay），支付成功回调时更新为 Paid
            _paymentService.CreatePrepay(order, ZR.Mall.Enum.PayTypeEnum.Wechat, prepay.H5Url ?? prepay.PrepayId);

            // H5：返回跳转链接，由前端 window.location.href 跳转
            if (!string.IsNullOrEmpty(prepay.H5Url))
            {
                return SUCCESS(new { order.OrderNo, order.OrderStatus, order.PayAmount, prepay.H5Url });
            }
            // 小程序/App：返回客户端调起支付所需的完整参数（含服务端签名的 paySign）
            return SUCCESS(new
            {
                order.OrderNo,
                order.OrderStatus,
                order.PayAmount,
                needClientPay = true,
                channel = prepay.Channel,
                payParams = new
                {
                    appId = prepay.AppId,
                    partnerId = prepay.PartnerId,
                    prepayId = prepay.PrepayId,
                    nonceStr = prepay.NonceStr,
                    timeStamp = prepay.TimeStamp,
                    package = prepay.Package,
                    signType = "RSA",
                    paySign = prepay.PaySign
                }
            });
        }

        /// <summary>
        /// 我的订单列表（分页，可按订单状态筛选）
        /// </summary>
        [HttpGet("list")]
        [MemberAuthorize]
        public IActionResult List([FromQuery] MemberOrderQueryDto parm)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var response = _orderService.GetMemberOrders(member.MemberId, parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 我的订单详情（不属于当前会员返回授权失败）
        /// </summary>
        [HttpGet("{id}")]
        [MemberAuthorize]
        public IActionResult Detail(long id)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var order = _orderService.GetMemberOrder(member.MemberId, id);
            if (order == null)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在或无权限");
            }
            return SUCCESS(order.Adapt<OMSOrderDto>());
        }

        /// <summary>
        /// 取消我的订单（仅待付款/待发货可取消，回补库存）
        /// </summary>
        [HttpPost("cancel/{id}")]
        [MemberAuthorize]
        public IActionResult Cancel(long id)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var rows = _orderService.CancelMemberOrder(member.MemberId, id);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "取消成功" : "取消失败");
        }
        /// <summary>
        /// 确认收货（仅已发货可确认；幂等，重复提交不报错）
        /// </summary>
        [HttpPost("confirm-receipt/{id}")]
        [MemberAuthorize]
        public IActionResult ConfirmReceipt(long id)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var rows = _orderService.ConfirmMemberReceipt(member.MemberId, id);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "确认收货成功" : "确认收货失败");
        }
    }
}
