using ZR.Model.Content;
using ZR.Model.Content.Dto;

namespace ZR.Service.Content.IService
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
        PagedInfo<ArticleDto> GetArticleList(ArticleQueryDto parm);
        PagedInfo<ArticleDto> GetMonentList(ArticleQueryDto parm);
        PagedInfo<ArticleDto> GetFollowMonentList(ArticleQueryDto parm);
        int TopArticle(Article model);
        int ChangeComment(Article model);
        int ChangeArticlePublic(Article model);
        int UpdateArticleHit(long cid);
        int PraiseArticle(long cid);
        int CancelPraise(long cid);
        Article PublishArticle(Article article);
        Article PublishMonent(Article article);

        ArticleDto GetArticle(long cid, long userId);
        int Passed(long[] idsArr);
        int Reject(string reason, long[] idsArr);
    }
}
