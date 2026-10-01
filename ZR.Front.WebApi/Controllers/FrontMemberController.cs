using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Controllers;
using Infrastructure.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZR.Model.Member.Dto;
using ZR.ServiceCore.Member;

namespace ZR.Front.WebApi.Controllers
{
    /// <summary>
    /// C 端会员接口（匿名可访问，会员身份由 mall-token 校验）。
    /// 与后台管理端账号/令牌完全隔离；首版仅支持手机号 + 短信验证码登录（登录即注册）。
    /// 会员登录属平台级 C 端能力，中立不绑任何业务模块，故置于公网宿主（项目约定：Controller 只放宿主或业务模块，框架/服务库不放）。
    /// 供商城、内容、社交等 C 端业务共用同一套会员身份。
    /// </summary>
    [PublicApi]
    [Route("front/member")]
    [ApiExplorerSettings(GroupName = "shopping")]
    [AllowAnonymous]
    public class FrontMemberController : BaseController
    {
        private readonly IMemberService _memberService;

        public FrontMemberController(IMemberService memberService)
        {
            _memberService = memberService;
        }

        /// <summary>
        /// 发送会员登录验证码（开发模式开启 MockSms:Enabled 时返回 devCode 便于联调）
        /// </summary>
        [HttpPost("send-code")]
        public IActionResult SendCode([FromBody] MemberSendCodeDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Phone))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号不能为空");
            }

            var ip = HttpContextExtension.GetClientUserIp(HttpContext);
            var log = _memberService.SendLoginCode(dto.Phone, ip);

            var mock = AppSettings.Get<bool>("MockSms:Enabled");
            var devCode = mock ? log.SmsCode : null;
            return SUCCESS(new { sent = true, devCode });
        }

        /// <summary>
        /// 会员登录：手机号 + 验证码，未注册手机号自动注册
        /// </summary>
        [HttpPost("login")]
        public IActionResult Login([FromBody] MemberLoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.Code))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "参数不完整");
            }

            var ip = HttpContextExtension.GetClientUserIp(HttpContext);
            var member = _memberService.LoginByPhone(dto.Phone, dto.Code, ip);
            var token = MemberJwt.GenerateToken(member);

            return SUCCESS(new
            {
                token,
                tokenHeader = MemberJwt.TokenHeader,
                expiresIn = MemberJwt.GetExpireSeconds(),
                member = member.Adapt<MemberInfoDto>()
            });
        }

        /// <summary>
        /// 当前登录会员信息
        /// </summary>
        [HttpGet("info")]
        [MemberAuthorize]
        public IActionResult Info()
        {
            var current = GetCurrentMember();
            var member = _memberService.GetById(current.MemberId);
            if (member == null || member.IsDelete != 0)
            {
                return ToResponse(ResultCode.OAUTH_FAIL, "会员不存在");
            }
            return SUCCESS(member.Adapt<MemberInfoDto>());
        }

        /// <summary>
        /// 更新会员资料（昵称/头像，仅更新传入的非空字段）
        /// </summary>
        [HttpPut("update")]
        [MemberAuthorize]
        public IActionResult Update([FromBody] MemberUpdateDto dto)
        {
            var current = GetCurrentMember();
            var rows = _memberService.UpdateProfile(current.MemberId, dto?.NickName, dto?.Avatar);
            if (rows <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有需要更新的内容");
            }

            var member = _memberService.GetById(current.MemberId);
            return SUCCESS(member.Adapt<MemberInfoDto>());
        }

        /// <summary>
        /// 读取当前请求的会员身份（由 MemberAuthorize 过滤器写入）
        /// </summary>
        private MemberToken GetCurrentMember()
        {
            return MemberAuthorizeAttribute.GetMember(HttpContext);
        }
    }
}
