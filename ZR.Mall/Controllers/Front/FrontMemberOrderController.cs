using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Common;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// C 端会员"我的订单"接口。
    /// 会员身份取自令牌（请求头 mall-token），无需再传手机号与验证码；
    /// 查询范围 = 会员本人下单 + 同手机号的历史游客订单（按手机号归属合并）。
    /// </summary>
    [PublicApi]
    [Route("front/member/order")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontMemberOrderController : BaseController
    {
        private readonly IFrontOrderService _orderService;

        public FrontMemberOrderController(IFrontOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// 我的订单列表（分页，可按订单状态筛选）
        /// </summary>
        [HttpGet("list")]
        [MemberAuthorize]
        public IActionResult List([FromQuery] MemberOrderQueryDto parm)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var response = _orderService.GetMemberOrders(member.MemberId, member.Phone, parm);
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
            var order = _orderService.GetMemberOrder(member.MemberId, member.Phone, id);
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
            var rows = _orderService.CancelMemberOrder(member.MemberId, member.Phone, id);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "取消成功" : "取消失败");
        }
    }
}
