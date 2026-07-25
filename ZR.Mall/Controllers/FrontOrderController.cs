using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using ZR.Mall.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using ZR.Common;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.Model.Models;
using ZR.ServiceCore.Services;

//创建时间：2026-07-25
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 商城C端/游客接口（匿名可访问，与后台 shopping/Order 区分）
    /// </summary>
    [Route("shopping/front/order")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontOrderController : BaseController
    {
        private readonly IOMSOrderService _OMSOrderService;
        private readonly ISmsCodeLogService _smsCodeLogService;

        public FrontOrderController(IOMSOrderService OMSOrderService, ISmsCodeLogService smsCodeLogService)
        {
            _OMSOrderService = OMSOrderService;
            _smsCodeLogService = smsCodeLogService;
        }

        /// <summary>
        /// 游客下单（无需登录）
        /// </summary>
        [HttpPost("create")]
        public IActionResult CreateOrder([FromBody] CreateOrderDto parm)
        {
            if (parm == null || parm.Items == null || parm.Items.Count == 0)
            {
                return ToResponse(ResultCode.PARAM_ERROR, "下单商品不能为空");
            }
            if (parm.Address == null || string.IsNullOrWhiteSpace(parm.Address.Phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "收货手机号不能为空");
            }
            // #7 下单前再校验手机号格式（与发码接口一致）
            if (!Regex.IsMatch(parm.Address.Phone, @"^1\d{10}$"))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号格式不正确");
            }
            // 下单身份校验：验证码必须与 Address.Phone 配对且通过，证明手机号归属。
            // 否则任何人可填他人手机号下单，后续 pay/cancel/查订单都会基于错误归属 —— 这是原漏洞根因。
            if (string.IsNullOrWhiteSpace(parm.Code) || !CacheService.CheckPhoneCode(parm.Address.Phone, parm.Code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "短信验证码错误或已过期，请重新获取");
            }
            CacheService.RemovePhoneCode(parm.Address.Phone); // 一次性校验，防重放
            // #2 游客下单防刷/限流（IP + 手机号维度）
            var ip = HttpContextExtension.GetClientUserIp(HttpContext);
            if (!TryAcquireCreateQuota(ip, parm.Address.Phone, out var quotaMsg))
            {
                return ToResponse(ResultCode.FAIL, quotaMsg);
            }
            var order = _OMSOrderService.CreateOrder(parm);
            return SUCCESS(new { order.OrderNo, order.Id, order.TotalAmount, order.OrderStatus });
        }

        /// <summary>
        /// 模拟支付（待付款 → 待发货）。
        /// 鉴权：订单号 + 下单手机号双重匹配（订单号仅下单者可见，无需再发验证码，保证支付流程顺畅）。
        /// 后期接入真实支付时，此接口改为发起支付，状态流转移到支付回调。
        /// </summary>
        [HttpPost("pay")]
        public IActionResult PayOrder([FromBody] FrontPayOrderDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.OrderNo) || string.IsNullOrWhiteSpace(dto.Phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "参数不完整");
            }
            var order = _OMSOrderService.PayOrder(dto.OrderNo, dto.Phone);
            return SUCCESS(new { order.OrderNo, order.OrderStatus, order.PayTime, order.PayAmount });
        }

        /// <summary>
        /// 游客取消自己的订单（需短信验证码校验归属），回补库存
        /// </summary>
        [HttpPost("cancel")]
        public IActionResult CancelOrder([FromBody] FrontCancelOrderDto dto)
        {
            if (dto == null || dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.Code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "参数不完整");
            }
            if (!CacheService.CheckPhoneCode(dto.Phone, dto.Code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "验证码错误或已过期");
            }
            CacheService.RemovePhoneCode(dto.Phone); // 一次性校验

            var order = _OMSOrderService.Queryable().First(x => x.Id == dto.Id && x.IsDelete == 0);
            if (order == null || order.AddressSnapshot?.Phone != dto.Phone)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在或无权限");
            }
            var res = _OMSOrderService.CancelOrder(dto.Id);
            return ToResponse(res > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, res > 0 ? "取消成功" : "取消失败");
        }

        /// <summary>
        /// #2 防刷限流：IP 与手机号双维度配额（10 分钟内）。返回 false 时 msg 为提示。
        /// 备注：AspNetCoreRateLimit 已有全局 POST 1次/3s 兜底，此处为业务级配额。
        /// </summary>
        private bool TryAcquireCreateQuota(string ip, string phone, out string msg)
        {
            msg = string.Empty;
            var ipKey = "mall_front_order_ip_" + ip;
            var ipCount = CacheHelper.Get(ipKey) is int ic ? ic : 0;
            if (ipCount >= 10)
            {
                msg = "下单过于频繁，请稍后再试";
                return false;
            }
            CacheHelper.SetCache(ipKey, ipCount + 1, 10);

            var phoneKey = "mall_front_order_phone_" + phone;
            var phoneCount = CacheHelper.Get(phoneKey) is int pc ? pc : 0;
            if (phoneCount >= 30)
            {
                msg = "该手机号下单过于频繁，请稍后再试";
                return false;
            }
            CacheHelper.SetCache(phoneKey, phoneCount + 1, 10);
            return true;
        }

        /// <summary>
        /// 发送查询订单验证码（证明手机号归属，防止查他人订单）
        /// 复用 SmsCodeLogService 生成验证码并缓存（与登录验证码机制一致）。
        /// 真实短信发送待接入短信服务商（项目 AddSmscodeLog 中 //TODO 发送验证码 同理）。
        /// </summary>
        [HttpPost("send-code")]
        public IActionResult SendQueryCode([FromBody] SendCodeDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Phone) || !Regex.IsMatch(dto.Phone, @"^1\d{10}$"))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号格式不正确");
            }
            // type: 4=查询订单（默认），5=下单。验证码按手机号缓存在同一键，校验时统一比对。
            var sendType = dto.Type == 5 ? 5 : 4;
            var ip = HttpContextExtension.GetClientUserIp(HttpContext);
            var log = _smsCodeLogService.AddSmscodeLog(new SmsCodeLog
            {
                PhoneNum = dto.Phone.ParseToLong(),
                SendType = sendType,
                UserIP = ip,
                Location = HttpContextExtension.GetIpInfo(ip)
            });
            // 真实短信发送待接入；开发期可开启 MockSms:Enabled 返回验证码便于联调
            var mock = App.Configuration["MockSms:Enabled"];
            var devCode = mock == "true" ? log.SmsCode : null;
            return SUCCESS(new { sent = true, devCode });
        }

        /// <summary>
        /// 游客按手机号查询自己的订单（需短信验证码校验归属）
        /// </summary>
        [HttpGet("list")]
        public IActionResult QueryMyOrders([FromQuery] FrontOrderQueryDto parm)
        {
            if (parm == null || string.IsNullOrWhiteSpace(parm.Phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号不能为空");
            }
            if (string.IsNullOrWhiteSpace(parm.Code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "请先获取并输入短信验证码");
            }
            if (!CacheService.CheckPhoneCode(parm.Phone, parm.Code))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "验证码错误或已过期");
            }
            CacheService.RemovePhoneCode(parm.Phone); // 一次性校验，防重放

            // GuestPhone 是一等公民列（下单时与验证码配对写入），可直接 SQL 过滤，
            // 避免此前“全表拉取 + 内存按 JSON 手机号过滤”的性能问题。
            var paged = _OMSOrderService.Queryable()
                .Includes(x => x.Items)
                .Where(it => it.IsDelete == 0 && it.GuestPhone == parm.Phone)
                .OrderBy(it => it.Id, OrderByType.Desc)
                .ToPage<OMSOrder, OMSOrderDto>(parm);
            return SUCCESS(paged);
        }

        /// <summary>
        /// 游客订单详情（按手机号+验证码校验归属）
        /// </summary>
        [HttpGet("{Id}")]
        public IActionResult GetOrder(long Id, [FromQuery] string phone, [FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号不能为空");
            }
            if (string.IsNullOrWhiteSpace(code) || !CacheService.CheckPhoneCode(phone, code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "请先获取并输入短信验证码");
            }
            CacheService.RemovePhoneCode(phone);

            var order = _OMSOrderService.Queryable()
                .Includes(x => x.Items)
                .Where(x => x.Id == Id && x.IsDelete == 0)
                .First();
            if (order == null || order.AddressSnapshot?.Phone != phone)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在");
            }
            return SUCCESS(order.Adapt<OMSOrderDto>());
        }

        /// <summary>
        /// 游客按订单号+手机号查询单笔订单（免验证码）。
        /// 鉴权模型与 pay 一致：订单号（雪花ID，仅下单者可见）+ 手机号（下单时验证码配对的 GuestPhone）双重匹配，
        /// 足以证明归属，无需再次发短信验证码。用于「下单/支付后立即可查看我的订单」的顺畅体验。
        /// </summary>
        [HttpGet("by-no")]
        public IActionResult GetOrderByNo([FromQuery] string orderNo, [FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "参数不完整");
            }
            var order = _OMSOrderService.Queryable()
                .Includes(x => x.Items)
                .Where(x => x.OrderNo == orderNo && x.IsDelete == 0)
                .First();
            if (order == null || order.GuestPhone != phone)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在或无权限");
            }
            return SUCCESS(order.Adapt<OMSOrderDto>());
        }
    }
}
