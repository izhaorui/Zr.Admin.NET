using Microsoft.AspNetCore.Mvc;
using ZR.Model.social;
using ZR.Service.Social.IService;

//创建时间：2025-07-25
namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 粉丝/关注
    /// </summary>
    [Route("Serviceapi/socialFans")]
    [ApiExplorerSettings(GroupName = "social")]
    [ApiController]
    public class SocialFansController : BaseController
    {
        private readonly ISocialFansService _socialFansService;

        public SocialFansController(ISocialFansService socialFansService)
        {
            _socialFansService = socialFansService;
        }

        /// <summary>
        /// 关注/粉丝列表
        /// </summary>
        [HttpGet("followList")]
        public IActionResult FollowList([FromQuery] FansQueryDto dto)
        {
            return ToResponse(_socialFansService.FollowList(dto));
        }

        /// <summary>
        /// 是否关注
        /// </summary>
        [HttpGet("IsFollow")]
        public IActionResult IsFollow([FromQuery] int toUserid)
        {
            return ToResponse(_socialFansService.IsFollow(toUserid));
        }

        /// <summary>
        /// 关注
        /// </summary>
        [HttpPost("Follow")]
        public IActionResult Follow([FromBody] SocialFansDto dto)
        {
            return ToResponse(_socialFansService.Follow(dto));
        }

        /// <summary>
        /// 取消关注
        /// </summary>
        [HttpPost("CancelFollow")]
        public IActionResult CancelFollow([FromBody] SocialFansDto dto)
        {
            return ToResponse(_socialFansService.CancelFollow(dto));
        }
    }
}
