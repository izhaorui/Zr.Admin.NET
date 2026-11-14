using Microsoft.AspNetCore.Mvc;
using ZR.Model.Content;
using ZR.Model.Content.Dto;
using ZR.Model.Enum;
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
        /// 查询我的
        /// </summary>
        /// <returns></returns>
        [HttpGet("mylist")]
        public IActionResult QueryMyList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            parm.ArticleType = (int)ArticleTypeEnum.Article;
            var response = _ArticleService.GetMyList(parm);

            return SUCCESS(response);
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
            if (parm == null || parm.Cid <= 0) { return ToResponse(ResultCode.PARAM_ERROR, "缺少内容Id"); }
            var userId = HttpContext.GetUId();
            if (userId <= 0) { return ToResponse(ResultCode.DENY); }

            // 归属校验下沉到 Service 的更新条件（where Cid=? and UserId=?），
            // 不能只比对请求体里的 userId —— 那等于没有校验。
            var response = _ArticleService.TopArticle(parm, userId);

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
            if (parm == null || parm.Cid <= 0) { return ToResponse(ResultCode.PARAM_ERROR, "缺少内容Id"); }
            var userId = HttpContext.GetUId();
            if (userId <= 0) { return ToResponse(ResultCode.DENY); }

            var response = _ArticleService.ChangeArticlePublic(parm, userId);

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
            if (parm == null || parm.Cid <= 0) { return ToResponse(ResultCode.PARAM_ERROR, "缺少内容Id"); }
            var userId = HttpContext.GetUId();
            if (userId <= 0) { return ToResponse(ResultCode.DENY); }

            var response = _ArticleService.ChangeComment(parm, userId);

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
        /// 编辑我的文章（仅作者本人，归属校验在 Service 内完成）
        /// </summary>
        /// <returns></returns>
        [HttpPut("edit")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult Edit([FromBody] ArticleDto parm)
        {
            if (parm == null || parm.Cid <= 0) { return ToResponse(ResultCode.PARAM_ERROR, "缺少内容Id"); }

            var userId = HttpContext.GetUId();
            if (userId <= 0) { return ToResponse(ResultCode.DENY); }

            var modal = parm.Adapt<Article>().ToUpdate(HttpContext);
            // 与 publish 保持一致：话题名作为标签；未传话题时由 Service 保留原标签
            modal.Tags = parm.TopicName;
            return SUCCESS(_ArticleService.UpdateMyArticle(modal, userId));
        }

        /// <summary>
        /// 我的创作统计（内容总数/已发布/草稿/私密/待审/驳回/浏览/点赞/评论）
        /// </summary>
        /// <returns></returns>
        [HttpGet("myStats")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult MyStats()
        {
            var userId = HttpContext.GetUId();
            if (userId <= 0) { return ToResponse(ResultCode.DENY); }

            return SUCCESS(_ArticleService.GetMyStats(userId));
        }

        /// <summary>
        /// 单篇内容统计（公开内容任何人可看，非公开内容仅作者本人可看；只读，不计浏览量）
        /// </summary>
        /// <param name="cid">内容id</param>
        /// <returns></returns>
        [HttpGet("stats/{cid}")]
        [AllowAnonymous]
        public IActionResult Stats(long cid)
        {
            var userId = HttpContext.GetUId();
            return SUCCESS(_ArticleService.GetArticleStats(cid, userId));
        }

        /// <summary>
        /// 站点内容统计（已发布文章数/今日新增/总浏览/总点赞/总评论，结果缓存 10 分钟）
        /// </summary>
        /// <returns></returns>
        [HttpGet("siteStats")]
        [AllowAnonymous]
        public IActionResult SiteStats()
        {
            return SUCCESS(_ArticleService.GetSiteStats());
        }

        /// <summary>
        /// 编辑前取数（仅作者本人，含正文；不会自增浏览量）
        /// </summary>
        /// <param name="cid">内容id</param>
        /// <returns></returns>
        [HttpGet("editInfo/{cid}")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult EditInfo(long cid)
        {
            var userId = HttpContext.GetUId();
            if (userId <= 0) { return ToResponse(ResultCode.DENY); }

            return SUCCESS(_ArticleService.GetMyArticleForEdit(cid, userId));
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
