using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Service.System.IService
{
    public interface IArticleService : IBaseService<Article>
    {
        PagedInfo<ArticleDto> GetList(ArticleQueryDto parm);
        /// <summary>
        /// 修改文章管理
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateArticle(Article model);
    }
}
