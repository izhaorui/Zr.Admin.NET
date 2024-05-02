using Infrastructure;
using Infrastructure.Attribute;
using ZR.ServiceCore.Model;
using ZR.ServiceCore.Services.IService;

namespace ZR.ServiceCore.Services
{
    [AppService(ServiceType = typeof(IArticlePraiseService), ServiceLifetime = LifeTime.Transient)]
    public class ArticlePraiseService : BaseService<ArticlePraise>, IArticlePraiseService
    {
        private IArticleService _articleService;

        public ArticlePraiseService(IArticleService articleService)
        {
            _articleService = articleService;
        }

        /// <summary>
        /// 点赞
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public int Praise(ArticlePraise dto)
        {
            int result = 0;
            var praiseInfo = GetFirst(f => f.UserId == dto.UserId && f.ArticleId == dto.ArticleId);
            var isPraise = 0;
            var r = UseTran(() =>
            {
                //第一次点赞插入数据
                if (praiseInfo == null)
                {
                    Insertable(dto).ExecuteReturnSnowflakeId();
                    result = _articleService.PraiseArticle(dto.ArticleId);
                    isPraise = 1;
                    //TODO 消息通知
                }
                else
                {
                    if (praiseInfo.IsDelete == 0)
                    {
                        result = CanclePraise(dto.UserId, dto.ArticleId);
                        //文章点赞-1
                        _articleService.CancelPraise(dto.ArticleId);
                    }
                    else
                    {
                        //文章点赞+1
                        _articleService.PraiseArticle(dto.ArticleId);
                        //恢复点赞为未删除状态
                        PlusPraise(praiseInfo.PId);
                        isPraise = 1;
                    }
                }
            });
            return r.IsSuccess && isPraise == 1 ? 1 : 0;
        }

        /// <summary>
        /// 取消点赞点赞
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="articleId"></param>
        /// <returns></returns>
        public int CanclePraise(long userid, long articleId)
        {
            return Deleteable()
                .Where(f => f.ArticleId == articleId && f.UserId == userid)
                .IsLogic()
                .ExecuteCommand(deleteValue: 1);
        }

        /// <summary>
        /// 恢复点赞
        /// </summary>
        /// <param name="pid"></param>
        /// <returns></returns>
        public int PlusPraise(long pid)
        {
            return Deleteable()
                .Where(f => f.PId == pid)
                .IsLogic()
                .ExecuteCommand(deleteValue: 0);
        }
    }
}
