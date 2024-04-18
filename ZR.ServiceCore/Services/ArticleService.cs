using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.ServiceCore.Model.Enums;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 
    /// </summary>
    [AppService(ServiceType = typeof(IArticleService), ServiceLifetime = LifeTime.Transient)]
    public class ArticleService : BaseService<Article>, IArticleService
    {
        /// <summary>
        /// 查询文章管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.AbstractText), m => m.AbstractText.Contains(parm.AbstractText));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Status), m => m.Status == parm.Status);
            predicate = predicate.AndIF(parm.IsPublic != null, m => m.IsPublic == parm.IsPublic);
            predicate = predicate.AndIF(parm.IsTop != null, m => m.IsTop == parm.IsTop);
            predicate = predicate.AndIF(parm.ArticleType != null, m => (int)m.ArticleType == parm.ArticleType);

            if (parm.CategoryId != null)
            {
                var allChildCategory = Context.Queryable<ArticleCategory>()
                    .ToChildList(m => m.ParentId, parm.CategoryId);
                var categoryIdList = allChildCategory.Select(x => x.CategoryId).ToArray();
                predicate = predicate.And(m => categoryIdList.Contains(m.CategoryId));
            }

            var response = Queryable()
                .WithCache(60 * 24)
                .IgnoreColumns(x => new { x.Content })
                .Includes(x => x.ArticleCategoryNav) //填充子对象
                .Where(predicate.ToExpression())
                //.OrderBy(x => x.CreateTime, OrderByType.Desc)
                .ToPage<Article, ArticleDto>(parm);

            return response;
        }

        /// <summary>
        /// 前台查询文章列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetHotList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.AndIF(parm.IsTop != null, m => m.IsTop == parm.IsTop);
            predicate = predicate.And(m => (int)m.ArticleType == parm.ArticleType);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));

            if (parm.CategoryId != null)
            {
                var allChildCategory = Context.Queryable<ArticleCategory>()
                    .ToChildList(m => m.ParentId, parm.CategoryId);
                var categoryIdList = allChildCategory.Select(x => x.CategoryId).ToArray();
                predicate = predicate.And(m => categoryIdList.Contains(m.CategoryId));
            }

            var response = Queryable()
                .WithCache(60 * 30)
                .Includes(x => x.ArticleCategoryNav)
                .LeftJoin<SysUser>((m, u) => m.UserId == u.UserId).Filter(null, true)
                .Where(predicate.ToExpression())
                .OrderByDescending(m => m.Cid)
                .Select((m, u) => new ArticleDto()
                {
                    Avatar = u.Avatar,
                    NickName = u.NickName,
                    Sex = u.Sex,
                    Content = string.Empty,
                    UserIP = string.Empty,
                    ArticleCategoryNav = m.ArticleCategoryNav
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 前台查询动态列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetMonentList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.And(m => m.ArticleType == ArticleTypeEnum.Monent);

            var response = Queryable()
                .LeftJoin<SysUser>((m, u) => m.UserId == u.UserId).Filter(null, true)
                .Where(predicate.ToExpression())
                .OrderByIF(parm.TabId == 3, m => new { m.PraiseNum }, OrderByType.Desc)
                .OrderBy(m => m.Cid, OrderByType.Desc)
                .Select((m, u) => new ArticleDto()
                {
                    Avatar = u.Avatar,
                    NickName = u.NickName,
                    Sex = u.Sex
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 查询我的文章列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetMyList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Status), m => m.Status == parm.Status);
            predicate = predicate.AndIF(parm.BeginTime != null, m => m.CreateTime >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime != null, m => m.CreateTime <= parm.EndTime);
            predicate = predicate.And(m => m.UserId == parm.UserId);
            predicate = predicate.AndIF(parm.ArticleType != null, m => (int)m.ArticleType == parm.ArticleType);
            predicate = predicate.AndIF(parm.TabId == 2, m => m.IsPublic == 0 && m.UserId == parm.UserId);

            var response = Queryable()
                //.IgnoreColumns(x => new { x.Content })
                .Includes(x => x.ArticleCategoryNav)
                .Where(predicate.ToExpression())
                .OrderByIF(parm.TabId == 3, m => new { m.PraiseNum }, OrderByType.Desc)
                .OrderByDescending(m => m.Cid)
                .Select((x) => new ArticleDto()
                {
                    Content = x.ArticleType == 0 ? string.Empty : x.Content,
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 修改文章管理
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateArticle(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                Title = model.Title,
                Content = model.Content,
                Status = model.Status,
                Tags = model.Tags,
                UpdateTime = DateTime.Now,
                CoverUrl = model.CoverUrl,
                CategoryId = model.CategoryId,
                FmtType = model.FmtType,
                IsPublic = model.IsPublic,
                AbstractText = model.AbstractText,
            });
            return response;
        }

        /// <summary>
        /// 置顶文章
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int TopArticle(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                IsTop = model.IsTop,
            });
            return response;
        }

        /// <summary>
        /// 是否公开
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int ChangeArticlePublic(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                IsPublic = model.IsPublic,
            });
            return response;
        }

        /// <summary>
        /// 修改文章访问量
        /// </summary>
        /// <param name="cid"></param>
        /// <returns></returns>
        public int UpdateArticleHit(int cid)
        {
            //var response = Context.Updateable<Article>()
            //    .SetColumns(it => it.Hits == it.Hits + 1)
            //    .Where(it => it.Cid == cid)
            //    .ExecuteCommand();
            var response = Update(w => w.Cid == cid, it => new Article() { Hits = it.Hits + 1 });
            return response;
        }

        /// <summary>
        /// 点赞
        /// </summary>
        /// <param name="cid"></param>
        /// <returns></returns>
        public int PraiseArticle(int cid)
        {
            return Context.Updateable<Article>()
                .SetColumns(it => it.PraiseNum == it.PraiseNum + 1)
                .Where(it => it.Cid == cid)
                .ExecuteCommand();
        }
    }
}
