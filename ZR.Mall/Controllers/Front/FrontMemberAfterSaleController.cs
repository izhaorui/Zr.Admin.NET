using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Member;

namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// C 端会员售后接口（申请 / 我的售后 / 详情 / 填退货物流 / 撤销）。
    ///
    /// 会员身份一律取自令牌（请求头 mall-token），归属只按 MemberId（见 <see cref="Common.OrderOwnership"/>）；
    /// 列表与详情由服务层强制按当前会员过滤，不采信前端传入的任何会员标识。
    /// </summary>
    [PublicApi]
    [Route("front/member/afterSale")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontMemberAfterSaleController : BaseController
    {
        private readonly IOMSAfterSaleService _afterSaleService;

        public FrontMemberAfterSaleController(IOMSAfterSaleService afterSaleService)
        {
            _afterSaleService = afterSaleService;
        }

        /// <summary>
        /// 申请售后（仅退款 / 退货退款）。未发货的订单只能选仅退款。
        /// </summary>
        [HttpPost("apply")]
        [MemberAuthorize]
        public IActionResult Apply([FromBody] AfterSaleApplyDto dto)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var id = _afterSaleService.Apply(member.MemberId, dto);
            return SUCCESS(new { id });
        }

        /// <summary>我的售后列表（按申请时间倒序）</summary>
        [HttpGet("list")]
        [MemberAuthorize]
        public IActionResult List([FromQuery] AfterSaleQueryDto parm)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            return SUCCESS(_afterSaleService.GetMemberList(member.MemberId, parm));
        }

        /// <summary>售后详情（校验归属）</summary>
        [HttpGet("{id}")]
        [MemberAuthorize]
        public IActionResult Detail(long id)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            return SUCCESS(_afterSaleService.GetMemberDetail(id, member.MemberId));
        }

        /// <summary>填写退货物流单号（待买家退货 → 待商家收货）</summary>
        [HttpPost("return")]
        [MemberAuthorize]
        public IActionResult FillReturn([FromBody] AfterSaleReturnDto dto)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var rows = _afterSaleService.FillReturnLogistics(member.MemberId, dto);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "提交成功" : "提交失败");
        }

        /// <summary>撤销售后申请（进入退款环节后不可撤销）</summary>
        [HttpPost("cancel/{id}")]
        [MemberAuthorize]
        public IActionResult Cancel(long id)
        {
            var member = MemberAuthorizeAttribute.GetMember(HttpContext);
            var rows = _afterSaleService.Cancel(id, member.MemberId);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "已撤销" : "撤销失败");
        }
    }
}
