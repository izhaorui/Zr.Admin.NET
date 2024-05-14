using ZR.Model.Content;

namespace ZR.Service.Content.IService
{
    public interface IArticlePraiseService : IBaseService<ArticlePraise>
    {
        int Praise(ArticlePraise dto);
        int CanclePraise(long userid, long articleId);
        int PlusPraise(long pid);
    }
}
