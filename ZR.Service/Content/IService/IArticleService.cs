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

        /// <summary>
        /// 前端-编辑我的内容（仅作者本人可操作），返回受影响行数
        /// </summary>
        int UpdateMyArticle(Article model, long userId);

        /// <summary>
        /// 我的创作统计（当前用户在 article 表中的全部内容）
        /// </summary>
        MyArticleStatsDto GetMyStats(long userId);

        /// <summary>
        /// 单篇内容统计（公开内容任何人可看，非公开内容仅作者本人可看）
        /// </summary>
        ArticleStatsDto GetArticleStats(long cid, long userId);

        /// <summary>
        /// 站点内容统计（前台展示用，结果按库缓存 10 分钟）
        /// </summary>
        SiteArticleStatsDto GetSiteStats();

        /// <summary>
        /// 前端-编辑前取数：返回自己的内容明细（含正文），不计浏览量、不写浏览日志
        /// </summary>
        ArticleDto GetMyArticleForEdit(long cid, long userId);
    }
}
