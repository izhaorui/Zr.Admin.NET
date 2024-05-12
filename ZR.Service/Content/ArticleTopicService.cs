using Infrastructure;
using Infrastructure.Attribute;
using Mapster;
using ZR.Model;
using ZR.Model.Content;
using ZR.Model.Content.Dto;
using ZR.Repository;
using ZR.Service.Content.IService;

namespace ZR.Service.Content
{
    /// <summary>
    /// 文章话题Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IArticleTopicService), ServiceLifetime = LifeTime.Transient)]
    public class ArticleTopicService : BaseService<ArticleTopic>, IArticleTopicService
    {
        /// <summary>
        /// 查询文章话题列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleTopicDto> GetList(ArticleTopicQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToPage<ArticleTopic, ArticleTopicDto>(parm);

            return response;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="TopicId"></param>
        /// <returns></returns>
        public ArticleTopic GetInfo(long TopicId)
        {
            var response = Queryable()
                .Where(x => x.TopicId == TopicId)
                .First();

            return response;
        }

        /// <summary>
        /// 添加文章话题
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ArticleTopic AddArticleTopic(ArticleTopic model)
        {
            if (Any(f => f.TopicName == model.TopicName))
            {
                throw new CustomException("话题名已存在");
            }
            return Insertable(model).ExecuteReturnEntity();
        }

        /// <summary>
        /// 修改文章话题
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateArticleTopic(ArticleTopic model)
        {
            return Update(model, true);
        }

        /// <summary>
        /// 导出文章话题
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleTopicDto> ExportList(ArticleTopicQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new ArticleTopicDto()
                {
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<ArticleTopic> QueryExp(ArticleTopicQueryDto parm)
        {
            var predicate = Expressionable.Create<ArticleTopic>();

            return predicate;
        }

        /// <summary>
        /// 查询热门文章话题列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public List<ArticleTopicDto> GetTopicList(ArticleTopicQueryDto parm)
        {
            var predicate = Expressionable.Create<ArticleTopic>();
            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToList();

            return response.Adapt<List<ArticleTopicDto>>();
        }
    }
}