using Microsoft.AspNetCore.Mvc;
using ZR.Model.Content;
using ZR.Model.Content.Dto;
using ZR.Service.Content.IService;
using ZR.Service.Social.IService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 内容管理前端接口
    /// </summary>
    [Route("front/article")]
    [ApiExplorerSettings(GroupName = "article")]
    public class FrontArticleController : BaseController
    {
        /// <summary>
        /// 文章接口
        /// </summary>
        private readonly IArticleService _ArticleService;
        private readonly IArticleCategoryService _ArticleCategoryService;
        private readonly IArticlePraiseService _ArticlePraiseService;
        private readonly ISysUserService _SysUserService;
        private readonly IArticleTopicService _ArticleTopicService;
        private readonly IArticleUserCirclesService _ArticleUserCirclesService;
        private readonly ISocialFansInfoService _FansInfoService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ArticleService"></param>
        /// <param name="articleCategoryService"></param>
        /// <param name="articlePraiseService"></param>
        /// <param name="sysUserService"></param>
        /// <param name="articleTopicService"></param>
        /// <param name="articleUserCirclesService"></param>
        /// <param name="fansInfoService"></param>
        public FrontArticleController(
            IArticleService ArticleService,
            IArticleCategoryService articleCategoryService,
            IArticlePraiseService articlePraiseService,
            ISysUserService sysUserService,
            IArticleTopicService articleTopicService,
            IArticleUserCirclesService articleUserCirclesService,
            ISocialFansInfoService fansInfoService)
        {
            _ArticleService = ArticleService;
            _ArticleCategoryService = articleCategoryService;
            _ArticleService = ArticleService;
            _ArticlePraiseService = articlePraiseService;
            _SysUserService = sysUserService;
            _ArticleTopicService = articleTopicService;
            _ArticleUserCirclesService = articleUserCirclesService;
            _FansInfoService = fansInfoService;
        }

        /// <summary>
        /// 前台查询文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("homeList")]
        [AllowAnonymous]
        public IActionResult QueryHomeList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            var response = _ArticleService.GetArticleList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询最新文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("newList")]
        [AllowAnonymous]
        public IActionResult QueryNew()
        {
            return SUCCESS(_ArticleService.GetNewArticleList());
        }

        /// <summary>
        /// 点赞
        /// </summary>
        /// <param name="id"></param>
        /// <param name="authorId"></param>
        /// <returns></returns>
        [HttpPost("praise/{id}")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Praise(int id = 0, long authorId = 0)
        {
            ArticlePraise addModel = new()
            {
                UserId = HttpContext.GetUId(),
                UserIP = HttpContext.GetClientUserIp(),
                ArticleId = id,
                ToUserId = authorId
            };
            addModel.Location = HttpContextExtension.GetIpInfo(addModel.UserIP);

            return SUCCESS(_ArticlePraiseService.Praise(addModel));
        }

        /// <summary>
        /// 置顶
        /// </summary>
        /// <returns></returns>
        [HttpPut("top")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Top([FromBody] Article parm)
        {
            var response = _ArticleService.TopArticle(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 修改可见范围
        /// </summary>
        /// <returns></returns>
        [HttpPut("changePublic")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult ChangePublic([FromBody] Article parm)
        {
            if (parm == null) { return ToResponse(ResultCode.CUSTOM_ERROR); }
            var userId = HttpContext.GetUId();
            if (userId != parm.UserId)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "操作失败");
            }
            var response = _ArticleService.ChangeArticlePublic(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 修改评论权限
        /// </summary>
        /// <returns></returns>
        [HttpPut("changeComment")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult ChangeComment([FromBody] Article parm)
        {
            if (parm == null) { return ToResponse(ResultCode.CUSTOM_ERROR); }
            var userId = HttpContext.GetUId();
            if (userId != parm.UserId)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "操作失败");
            }
            var response = _ArticleService.ChangeComment(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <returns></returns>
        [HttpDelete("del/{id}")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Delete(long id = 0)
        {
            var userId = HttpContext.GetUId();
            var info = _ArticleService.GetId(id);
            if (info == null || info.UserId != userId)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "删除失败(-1)");
            }
            var response = _ArticleService.Delete(id);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询文章详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public IActionResult Get(int id)
        {
            long userId = HttpContext.GetUId();
            var model = _ArticleService.GetArticle(id, userId);
            var user = _SysUserService.GetById(model.UserId);
            ApiResult apiResult = ApiResult.Success(model);
            model.User = new ArticleUser()
            {
                Avatar = user.Avatar,
                NickName = user.NickName,
                Sex = user.Sex,
            };

            return ToResponse(apiResult);
        }

        /// <summary>
        /// 前台查询话题
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("topicList")]
        [AllowAnonymous]
        public IActionResult QueryTopicList([FromQuery] ArticleTopicQueryDto parm)
        {
            var response = _ArticleTopicService.GetTopicList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询文章话题详情
        /// </summary>
        /// <param name="TopicId"></param>
        /// <returns></returns>
        [HttpGet("topic/{TopicId}")]
        [AllowAnonymous]
        public IActionResult GetArticleTopic(long TopicId)
        {
            var response = _ArticleTopicService.GetInfo(TopicId);

            _ArticleTopicService.Update(w => w.TopicId == TopicId, it => new ArticleTopic() { ViewNum = it.ViewNum + 1 });

            var info = response.Adapt<ArticleTopicDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 查询文章目录详情
        /// </summary>
        /// <param name="CategoryId"></param>
        /// <returns></returns>
        [HttpGet("QueryCircle/{CategoryId}")]
        [AllowAnonymous]
        public IActionResult GetArticleCategory(int CategoryId)
        {
            var userId = HttpContext.GetUId();
            var info = _ArticleCategoryService.GetFirst(x => x.CategoryId == CategoryId);
            var infoDto = info.Adapt<CircleInfoDto>();
            if (infoDto != null)
            {
                infoDto.IsJoin = _ArticleUserCirclesService.IsJoin((int)userId, CategoryId);
            }
            return SUCCESS(infoDto);
        }

        /// <summary>
        /// 查询圈子内容列表（文章+动态混合）
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("circleList")]
        [AllowAnonymous]
        public IActionResult GetCircleList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            var response = _ArticleService.GetCircleList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 发布文章
        /// </summary>
        /// <returns></returns>
        [HttpPost("publish")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Create([FromBody] ArticleDto parm)
        {
            if (parm == null) { return ToResponse(ResultCode.PARAM_ERROR); }
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            addModel.ArticleType = Model.Enum.ArticleTypeEnum.Article;
            addModel.Tags = parm.TopicName;
            return SUCCESS(_ArticleService.Publish(addModel));
        }

        /// <summary>
        /// 获取我的成就信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetUserWidget")]
        public IActionResult GetUserWidget(int toUserid)
        {
            var userId = HttpContext.GetUId();
            var fansInfo = _FansInfoService.GetFirst(f => f.Userid == toUserid) ?? new Model.social.SocialFansInfo() { };

            return SUCCESS(new { fansInfo });
        }
    }
}
