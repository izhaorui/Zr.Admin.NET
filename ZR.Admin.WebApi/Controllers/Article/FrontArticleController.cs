using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Model;
using ZR.ServiceCore.Services.IService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 内容管理前端接口
    /// </summary>
    [Route("front/article")]
    [Verify]
    public class FrontArticleController : BaseController
    {
        /// <summary>
        /// 文章接口
        /// </summary>
        private readonly IArticleService _ArticleService;
        private readonly IArticleCategoryService _ArticleCategoryService;
        private readonly IArticlePraiseService _ArticlePraiseService;
        private readonly ISysUserService _SysUserService;

        public FrontArticleController(
            IArticleService ArticleService,
            IArticleCategoryService articleCategoryService,
            IArticlePraiseService articlePraiseService,
            ISysUserService sysUserService)
        {
            _ArticleService = ArticleService;
            _ArticleCategoryService = articleCategoryService;
            _ArticleService = ArticleService;
            _ArticlePraiseService = articlePraiseService;
            _SysUserService = sysUserService;
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
            var response = _ArticleService.GetHotList(parm);

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
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.And(m => m.ArticleType == 0);

            var response = _ArticleService.Queryable()
                .Where(predicate.ToExpression())
                .Includes(x => x.ArticleCategoryNav) //填充子对象
                .Take(10)
                .OrderBy(f => f.UpdateTime, OrderByType.Desc).ToList();

            return SUCCESS(response);
        }

        /// <summary>
        /// 点赞
        /// </summary>
        /// <param name="id"></param>
        /// <param name="authorId"></param>
        /// <returns></returns>
        [HttpPost("praise/{id}")]
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
        /// 删除
        /// </summary>
        /// <returns></returns>
        [HttpDelete("del/{id}")]
        public IActionResult Delete(int id = 0)
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

            model.NickName = user.NickName;
            model.Avatar = user.Avatar;
            model.Sex = user.Sex;
            apiResult.Put("user", user.Adapt<SysUserDto>());
            return ToResponse(apiResult);
        }

    }
}
