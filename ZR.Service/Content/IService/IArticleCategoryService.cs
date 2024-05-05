using ZR.Model;
using ZR.Model.Content;
using ZR.Model.Dto;

namespace ZR.Service.Content.IService
{
    public interface IArticleCategoryService : IBaseService<ArticleCategory>
    {
        PagedInfo<ArticleCategory> GetList(ArticleCategoryQueryDto parm);
        List<ArticleCategory> GetTreeList(ArticleCategoryQueryDto parm);
        int AddArticleCategory(ArticleCategory parm);
    }
}
