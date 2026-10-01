using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.ServiceCore.Member;

namespace ZR.Mall.Controllers.Front
{
    /// <summary>
    /// C 端会员收货地址簿接口。
    /// 全部动作需携带会员令牌（请求头 mall-token）；归属一律取自令牌，不接收前端传入的会员 Id。
    /// </summary>
    [PublicApi]
    [Route("front/member/address")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [MemberAuthorize]
    public class FrontMemberAddressController : BaseController
    {
        private readonly IMemberAddressService _addressService;

        public FrontMemberAddressController(IMemberAddressService addressService)
        {
            _addressService = addressService;
        }

        /// <summary>
        /// 我的地址列表（默认地址排最前）
        /// </summary>
        [HttpGet("list")]
        public IActionResult List()
        {
            var memberId = CurrentMemberId();
            var list = _addressService.GetList(memberId);
            return SUCCESS(list.Adapt<List<MemberAddressInfoDto>>());
        }

        /// <summary>
        /// 新增收货地址（首条自动设为默认）
        /// </summary>
        [HttpPost("add")]
        public IActionResult Add([FromBody] MemberAddressDto dto)
        {
            var memberId = CurrentMemberId();
            var entity = _addressService.Add(memberId, dto);
            return SUCCESS(entity.Adapt<MemberAddressInfoDto>());
        }

        /// <summary>
        /// 修改收货地址（仅能修改自己的地址）
        /// </summary>
        [HttpPut("update")]
        public IActionResult Update([FromBody] MemberAddressDto dto)
        {
            var memberId = CurrentMemberId();
            var rows = _addressService.UpdateAddress(memberId, dto);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "修改成功" : "修改失败");
        }

        /// <summary>
        /// 删除收货地址（删除默认地址后自动把最新一条设为默认）
        /// </summary>
        [HttpPost("delete/{id}")]
        public IActionResult Delete(long id)
        {
            var memberId = CurrentMemberId();
            var rows = _addressService.DeleteAddress(memberId, id);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "删除成功" : "删除失败");
        }

        /// <summary>
        /// 设为默认收货地址
        /// </summary>
        [HttpPost("set-default/{id}")]
        public IActionResult SetDefault(long id)
        {
            var memberId = CurrentMemberId();
            var rows = _addressService.SetDefault(memberId, id);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "设置成功" : "设置失败");
        }

        /// <summary>
        /// 当前会员Id（MemberAuthorize 已保证非空）
        /// </summary>
        private long CurrentMemberId()
        {
            return MemberAuthorizeAttribute.GetMember(HttpContext)?.MemberId ?? 0;
        }
    }
}
