using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    public interface IArticleService : IBaseService<Article>
    {
        PagedInfo<ArticleDto> GetList(ArticleQueryDto parm);
        PagedInfo<ArticleDto> GetMyList(ArticleQueryDto parm);
        /// <summary>
        /// 修改文章管理
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateArticle(Article model);
        PagedInfo<ArticleDto> GetHotList(ArticleQueryDto parm);
        PagedInfo<ArticleDto> GetMonentList(ArticleQueryDto parm);
        int TopArticle(Article model);
        int ChangeArticlePublic(Article model);
        int UpdateArticleHit(int cid);
        int PraiseArticle(int cid);
        Article PublishArticle(Article article);
    }
}
