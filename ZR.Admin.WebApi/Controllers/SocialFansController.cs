using Microsoft.AspNetCore.Mvc;
using ZR.Model.social;
using ZR.Service.Social.IService;
using ZR.ServiceCore.Middleware;

//创建时间：2025-07-25
namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 粉丝/关注
    /// </summary>
    /// <remarks>
    /// Permission = common 表示"登录用户即可访问"（见 ActionPermissionFilter）。
    /// 此前未标注任何权限特性，属于隐式放行；显式标注后语义明确，
    /// 且 TenantFeatureMiddleware 不再把它当成"未标注接口"跳过套餐校验。
    /// 接口自身仍只操作当前登录用户的数据（关注/取消关注）。
    /// </remarks>
    [Route("Serviceapi/socialFans")]
    [ApiExplorerSettings(GroupName = "social")]
    [ApiController]
    [ActionPermissionFilter(Permission = "common")]
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
