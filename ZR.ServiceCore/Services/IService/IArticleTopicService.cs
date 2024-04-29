using ZR.Model;
using ZR.ServiceCore.Model;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services.IService
{
    /// <summary>
    /// 文章话题service接口
    /// </summary>
    public interface IArticleTopicService : IBaseService<ArticleTopic>
    {
        PagedInfo<ArticleTopicDto> GetList(ArticleTopicQueryDto parm);

        ArticleTopic GetInfo(long TopicId);


        ArticleTopic AddArticleTopic(ArticleTopic parm);
        int UpdateArticleTopic(ArticleTopic parm);

        List<ArticleTopicDto> GetTopicList(ArticleTopicQueryDto parm);
        PagedInfo<ArticleTopicDto> ExportList(ArticleTopicQueryDto parm);
    }
}
