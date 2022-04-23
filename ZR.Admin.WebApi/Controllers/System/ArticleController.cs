using Infrastructure.Attribute;
using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Service.System.IService;
using Infrastructure.Model;
using SqlSugar;
using Mapster;
using ZR.Model.System.Dto;
using Infrastructure.Enums;
using Infrastructure;
using ZR.Admin.WebApi.Extensions;
using System.Reflection;
using System;

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
            //开始拼装查询条件
            var predicate = Expressionable.Create<Article>();

            //搜索条件
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Status), m => m.Status == parm.Status);

            var response = _ArticleService.GetPages(predicate.ToExpression(), parm, f => f.Cid, OrderByType.Desc);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询最新文章列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("newList")]
        public IActionResult QueryNew()
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<Article>();

            //搜索条件
            predicate = predicate.And(m => m.Status == "1");

            var response = _ArticleService.Queryable()
                .Where(predicate.ToExpression())
                .Take(10)
                .OrderBy(f => f.UpdateTime, OrderByType.Desc).ToList();

            return SUCCESS(response);
        }

        /// <summary>
        /// 获取文章目录,前端没用到
        /// </summary>
        /// <returns></returns>
        [HttpGet("CategoryList")]
        public IActionResult CategoryList()
        {
            var response = _ArticleCategoryService.GetAll();
            return SUCCESS(response);
        }

        /// <summary>
        /// 获取文章目录树
        /// </summary>
        /// <returns></returns>
        [HttpGet("CategoryTreeList")]
        public IActionResult CategoryTreeList()
        {
            var response = _ArticleCategoryService.BuildCategoryTree(_ArticleCategoryService.GetAll());
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询文章详情
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var response = _ArticleService.GetId(id);

            return SUCCESS(response);
        }

        /// <summary>
        /// 添加文章
        /// </summary>
        /// <returns></returns>
        [HttpPost("add")]
        [ActionPermissionFilter(Permission = "system:article:add")]
        [Log(Title = "文章添加", BusinessType = BusinessType.INSERT)]
        public IActionResult Create([FromBody] Article parm)
        {
            if (parm == null)
            {
                throw new CustomException("请求参数错误");
            }
            //从 Dto 映射到 实体
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            addModel.AuthorName = User.Identity.Name;

            return SUCCESS(_ArticleService.Add(addModel));
        }

        /// <summary>
        /// 更新文章
        /// </summary>
        /// <returns></returns>
        [HttpPut("edit")]
        [ActionPermissionFilter(Permission = "system:article:update")]
        [Log(Title = "文章修改", BusinessType = BusinessType.UPDATE)]
        public IActionResult Update([FromBody] Article parm)
        {
            //从 Dto 映射到 实体
            var addModel = parm.Adapt<Article>().ToCreate(context: HttpContext);
            addModel.AuthorName = HttpContext.GetName();

            var response = _ArticleService.Update(it => it.Cid == addModel.Cid,
                f => new Article
                {
                    Title = addModel.Title,
                    Content = addModel.Content,
                    Tags = addModel.Tags,
                    Category_Id = addModel.Category_Id,
                    UpdateTime = addModel.UpdateTime,
                    Status = addModel.Status
                }).ToCreate();

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