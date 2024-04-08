using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Model.Enums;

namespace ZR.Admin.WebApi.Controllers
{
    [Verify]
    [Route("monent")]
    public class MonentController : BaseController
    {
        /// <summary>
        /// 动态接口
        /// </summary>
        private readonly IArticleService _ArticleService;
        private readonly IArticleCategoryService _ArticleCategoryService;

        public MonentController(IArticleService ArticleService, IArticleCategoryService articleCategoryService)
        {
            _ArticleService = ArticleService;
            _ArticleCategoryService = articleCategoryService;
        }

        /// <summary>
        /// 查询我的
        /// </summary>
        /// <returns></returns>
        [HttpGet("mylist")]
        public IActionResult QueryMyList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            parm.ArticleType = 2;
            var response = _ArticleService.GetMyList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询动态列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("monentList")]
        [AllowAnonymous]
        public IActionResult QueryMonentList([FromQuery] ArticleQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            parm.ArticleType = 2;
            var response = _ArticleService.GetArticleList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 置顶
        /// </summary>
        /// <returns></returns>
        [HttpPut("top")]
        [Log(Title = "置顶动态", BusinessType = BusinessType.UPDATE)]
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
        [Log(Title = "是否公开", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangePublic([FromBody] Article parm)
        {
            var response = _ArticleService.ChangeArticlePublic(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 动态发布
        /// </summary>
        /// <returns></returns>
        [HttpPost("publishMonent")]
        [Log(Title = "动态发布", BusinessType = BusinessType.INSERT)]
        public IActionResult PublishMonent([FromBody] ArticleDto parm)
        {
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            addModel.AuthorName = HttpContext.GetName();
            addModel.UserId = HttpContext.GetUId();
            addModel.ArticleType = ArticleTypeEnum.Monent;
            addModel.UserIP = HttpContext.GetClientUserIp();

            string location = HttpContextExtension.GetIpInfo(addModel.UserIP);
            addModel.Location = location;
            return SUCCESS(_ArticleService.InsertReturnIdentity(addModel));
        }

        /// <summary>
        /// 删除动态
        /// </summary>
        /// <returns></returns>
        [HttpDelete("del/{id}")]
        [Log(Title = "动态删除", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteMonents(int id = 0)
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
        /// 点赞动态
        /// </summary>
        /// <returns></returns>
        [HttpPost("praise/{id}")]
        public IActionResult PraiseMonents(int id = 0)
        {
            var response = _ArticleService.PraiseArticle(id);
            return SUCCESS(response);
        }

        /// <summary>
        /// 动态信息
        /// </summary>
        /// <returns></returns>
        [HttpDelete("getInfo")]
        public IActionResult GetInfo()
        {
            var userId = HttpContext.GetUId();

            var monentNum = _ArticleService.Queryable()
                .Count(f => f.UserId == userId && f.ArticleType == ArticleTypeEnum.Monent);

            return SUCCESS(new { monentNum, commentNum = 0 });
        }
    }
}
