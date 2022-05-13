using System.Collections.Generic;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface IArticleCategoryService : IBaseService<ArticleCategory>
    {
        PagedInfo<ArticleCategory> GetList(ArticleCategoryQueryDto parm);
        List<ArticleCategory> GetTreeList(ArticleCategoryQueryDto parm);
        int AddArticleCategory(ArticleCategory parm);
    }
}
