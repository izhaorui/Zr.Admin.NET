using Infrastructure.Attribute;
using SqlSugar;
using SqlSugar.IOC;
using System.Collections.Generic;
using System.Linq;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.System;
using ZR.Repository;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 文章目录Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IArticleCategoryService), ServiceLifetime = LifeTime.Transient)]
    public class ArticleCategoryService : BaseService<ArticleCategory>, IArticleCategoryService
    {
        private readonly ArticleCategoryRepository _ArticleCategoryRepository;
        public ArticleCategoryService(ArticleCategoryRepository repository)
        {
            _ArticleCategoryRepository = repository;
        }

        /// <summary>
        /// 查询文章目录列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleCategory> GetList(ArticleCategoryQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<ArticleCategory>();

            //搜索条件查询语法参考Sqlsugar
            var response = _ArticleCategoryRepository
                .Queryable()
                .Where(predicate.ToExpression())
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 查询文章目录树列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public List<ArticleCategory> GetTreeList(ArticleCategoryQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<ArticleCategory>();

            //搜索条件查询语法参考Sqlsugar

            var response = _ArticleCategoryRepository.Queryable().Where(predicate.ToExpression())
                .ToTree(it => it.Children, it => it.ParentId, 0);

            return response;
        }

        /// <summary>
        /// 添加文章目录
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int AddArticleCategory(ArticleCategory parm)
        {
            var response = _ArticleCategoryRepository.Insert(parm, it => new
            {
                it.Name,
                it.CreateTime,
                it.ParentId,
            });
            return response;
        }
    }
}
