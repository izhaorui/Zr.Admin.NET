using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model.System.Dto;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 文章管理
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class ArticleRepository : BaseRepository<Article>
    {
        
    }

    /// <summary>
    /// 文章目录
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class ArticleCategoryRepository : BaseRepository<ArticleCategory>
    {

    }
}
