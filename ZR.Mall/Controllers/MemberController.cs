using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Model.Member.Dto;
using ZR.ServiceCore.Member;

namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 会员管理（后台）。
    /// 会员身份为平台级能力（ums_member，与 sys_user 完全隔离），管理入口置于商城后台。
    /// 只读 + 启停状态管理：会员由 C 端注册产生，后台不提供新增/删除（软删暂不开放后台操作）。
    /// </summary>
    [Route("shopping/member")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class MemberController : BaseController
    {
        /// <summary>
        /// 会员管理接口
        /// </summary>
        private readonly IMemberService _memberService;

        public MemberController(IMemberService memberService)
        {
            _memberService = memberService;
        }

        /// <summary>
        /// 查询会员列表（分页）
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "shop:member:list")]
        public IActionResult QueryMember([FromQuery] MemberQueryDto parm)
        {
            var response = _memberService.GetPageList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询会员详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "shop:member:query")]
        public IActionResult GetMember(long id)
        {
            var member = _memberService.GetById(id);
            if (member == null || member.IsDelete != 0)
            {
                return ToResponse(ResultCode.FAIL, "会员不存在");
            }
            return SUCCESS(member);
        }

        /// <summary>
        /// 启用/禁用会员。禁用后其 mall-token 经鉴权状态复核最迟 60 秒内失效。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPut("changeStatus")]
        [ActionPermissionFilter(Permission = "shop:member:edit")]
        [Log(Title = "会员启用/禁用", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeMemberStatus([FromBody] MemberStatusDto dto)
        {
            if (dto == null || dto.MemberId <= 0)
            {
                return ToResponse(ResultCode.PARAM_ERROR, "参数错误");
            }
            var rows = _memberService.SetStatus(dto.MemberId, dto.Status);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "操作成功" : "会员不存在");
        }
    }
}
