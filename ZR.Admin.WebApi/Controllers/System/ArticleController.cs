using Aliyun.OSS;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 文章管理
    /// </summary>
    [Verify]
    [Route("article")]
    public class ArticleController : BaseController
    {
        /// <summary>
        /// 文章接口
        /// </summary>
        private readonly IArticleService _ArticleService;
        private readonly IArticleCategoryService _ArticleCategoryService;

        public ArticleController(IArticleService ArticleService, IArticleCategoryService articleCategoryService)
        {
            _ArticleService = ArticleService;
            _ArticleCategoryService = articleCategoryService;
        }

        /// <summary>
        /// 查询文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:article:list")]
        public IActionResult Query([FromQuery] ArticleQueryDto parm)
        {
            var response = _ArticleService.GetList(parm);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询最新文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("newList")]
        public IActionResult QueryNew()
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");

            var response = _ArticleService.Queryable()
                .Where(predicate.ToExpression())
                .Includes(x => x.ArticleCategoryNav) //填充子对象
                .Take(10)
                .OrderBy(f => f.UpdateTime, OrderByType.Desc).ToList();

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
            var response = _ArticleService.GetId(id);
            var model = response.Adapt<ArticleDto>();
            if (model != null)
            {
                model.ArticleCategoryNav = _ArticleCategoryService.GetById(model.CategoryId);
                model.TagList = model.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }
            return SUCCESS(model);
        }

        /// <summary>
        /// 添加文章
        /// </summary>
        /// <returns></returns>
        [HttpPost("add")]
        [ActionPermissionFilter(Permission = "system:article:add")]
        [Log(Title = "文章添加", BusinessType = BusinessType.INSERT)]
        public IActionResult Create([FromBody] ArticleDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            addModel.AuthorName = HttpContext.GetName();

            return SUCCESS(_ArticleService.Add(addModel));
        }

        /// <summary>
        /// 更新文章
        /// </summary>
        /// <returns></returns>
        [HttpPut("edit")]
        [ActionPermissionFilter(Permission = "system:article:update")]
        [Log(Title = "文章修改", BusinessType = BusinessType.UPDATE)]
        public IActionResult Update([FromBody] ArticleDto parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            parm.AuthorName = HttpContext.GetName();
            var modal = parm.Adapt<Article>().ToUpdate(HttpContext);

            var response = _ArticleService.UpdateArticle(modal);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除文章
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ActionPermissionFilter(Permission = "system:article:delete")]
        [Log(Title = "文章删除", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(int id = 0)
        {
            if (id <= 0)
            {
                return ToResponse(ApiResult.Error($"删除失败Id 不能为空"));
            }

            // 删除文章
            var response = _ArticleService.Delete(id);

            return SUCCESS(response);
        }

    }
}