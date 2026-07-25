using ZR.Model.Content;
using ZR.Model.Content.Dto;

namespace ZR.Service.Content.IService
{
    public interface IArticleService : IBaseService<Article>
    {
        PagedInfo<ArticleDto> GetList(ArticleQueryDto parm);
        PagedInfo<ArticleDto> GetMyList(ArticleQueryDto parm);
        public int UpdateArticle(Article model);
        PagedInfo<ArticleDto> GetArticleList(ArticleQueryDto parm);
        List<ArticleDto> GetNewArticleList();
        PagedInfo<ArticleDto> GetMonentList(ArticleQueryDto parm);
        /// <summary>
        /// 查询圈子内容列表（文章+动态混合，按分类含子分类）
        /// </summary>
        PagedInfo<ArticleDto> GetCircleList(ArticleQueryDto parm);
        int TopArticle(Article model);
        int ChangeComment(Article model);
        int ChangeArticlePublic(Article model);
        int UpdateArticleHit(long cid);
        int PraiseArticle(long cid);
        int CancelPraise(long cid);
        Article PublishArticle(Article article);
        Article Publish(Article article);

        ArticleDto GetArticle(long cid, long userId);
        int Passed(long[] idsArr);
        int Reject(string reason, long[] idsArr);

        List<ArticleExportDto> ExportList(ArticleQueryDto parm);
        int ImportArticle(List<ArticleImportDto> list);
    }
}
