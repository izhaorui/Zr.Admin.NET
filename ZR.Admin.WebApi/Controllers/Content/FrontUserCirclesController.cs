using Microsoft.AspNetCore.Mvc;
using ZR.Service.Content.IService;
using ZR.ServiceCore.Middleware;

//创建时间：2025-04-07
namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 用户加入圈子
    /// </summary>
    /// <remarks>
    /// Permission = common 表示"登录用户即可访问"（见 ActionPermissionFilter）。
    /// 此前未标注任何权限特性，属于隐式放行；显式标注后语义明确。
    /// 三个接口均以 HttpContext.GetUId() 取当前登录用户，只操作本人的圈子关系，不越权访问他人数据。
    /// </remarks>
    [Route("front/circles")]
    [ApiExplorerSettings(GroupName = "userCircle")]
    [ApiController]
    [ActionPermissionFilter(Permission = "common")]
    public class ArticleUserCirclesController : BaseController
    {
        /// <summary>
        /// 用户加入圈子接口
        /// </summary>
        private readonly IArticleUserCirclesService _ArticleUserCirclesService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ArticleUserCirclesService"></param>
        public ArticleUserCirclesController(
            IArticleUserCirclesService ArticleUserCirclesService)
        {
            _ArticleUserCirclesService = ArticleUserCirclesService;
        }

        /// <summary>
        /// 查询用户加入圈子列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("myCircleList")]
        public IActionResult QueryArticleUserCircles()
        {
            var userId = HttpContext.GetUId();
            var response = _ArticleUserCirclesService.GetMyJoinCircles((int)userId);
            return SUCCESS(response);
        }

        /// <summary>
        /// 用户加入圈子
        /// </summary>
        /// <returns></returns>
        [HttpPost("join/{id}")]
        public IActionResult JoinCircles([FromRoute] int id)
        {
            var userId = HttpContext.GetUId();
            return ToResponse(_ArticleUserCirclesService.JoinCircle((int)userId, id));
        }

        /// <summary>
        /// 删除用户加入圈子
        /// </summary>
        /// <returns></returns>
        [HttpPost("delete/{id}")]
        public IActionResult DeleteCircles([FromRoute] int id)
        {
            var userId = HttpContext.GetUId();
            return ToResponse(_ArticleUserCirclesService.RemoveCircle((int)userId, id));
        }
    }
}