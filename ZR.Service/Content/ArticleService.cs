using System.Net;
using System.Text.RegularExpressions;
using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using Mapster;
using ZR.Common;
using ZR.Model.Content;
using ZR.Model.Content.Dto;
using ZR.Model.Enum;
using ZR.Model.System;
using ZR.Repository;
using ZR.Service.Content.IService;
using ZR.ServiceCore.Services;

namespace ZR.Service.Content
{
    [AppService(ServiceType = typeof(IArticleService), ServiceLifetime = LifeTime.Transient)]
    public class ArticleService : BaseService<Article>, IArticleService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>文章正文全文索引名，与 document/doc/文章正文全文检索改造.md 中的脚本保持一致</summary>
        private const string ArticleContentFtsIndex = "ft_article_content";

        private readonly IArticleCategoryService _categoryService;
        private readonly IArticleTopicService _topicService;
        private readonly ISysUserMsgService _userMsgService;

        public ArticleService(
            IArticleCategoryService categoryService,
            IArticleTopicService topicService,
            ISysUserMsgService userMsgService)
        {
            _categoryService = categoryService;
            _topicService = topicService;
            _userMsgService = userMsgService;
        }

        /// <summary>
        /// 查询文章管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.AbstractText), m => m.AbstractText.Contains(parm.AbstractText));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Status), m => m.Status == parm.Status);
            predicate = predicate.AndIF(parm.IsPublic != null, m => m.IsPublic == parm.IsPublic);
            predicate = predicate.AndIF(parm.IsTop != null, m => m.IsTop == parm.IsTop);
            predicate = predicate.AndIF(parm.ArticleType != null, m => (int)m.ArticleType == parm.ArticleType);
            predicate = predicate.AndIF(parm.TopicId != null, m => m.TopicId == parm.TopicId);
            predicate = predicate.AndIF(parm.AuditStatus != null, m => m.AuditStatus == parm.AuditStatus);
            predicate = predicate.AndIF(parm.BeginTime != null, m => m.CreateTime >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime != null, m => m.CreateTime <= parm.EndTime);

            if (parm.CategoryId != null)
            {
                var allChildCategory = Context.Queryable<ArticleCategory>()
                    .ToChildList(m => m.ParentId, parm.CategoryId);
                var categoryIdList = allChildCategory.Select(x => x.CategoryId).ToArray();
                predicate = predicate.And(m => categoryIdList.Contains(m.CategoryId));
            }

            var response = Queryable()
                .WithCache(60 * 24)
                .IgnoreColumns(x => new { x.Content })
                .Includes(x => x.CategoryNav) //填充子对象
                .Where(predicate.ToExpression())
                //.OrderBy(x => x.CreateTime, OrderByType.Desc)
                .ToPage<Article, ArticleDto>(parm);

            return response;
        }

        /// <summary>
        /// 前台查询文章列表（支持关键词检索：标题/摘要 + 正文）
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetArticleList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.And(m => m.AuditStatus == AuditStatusEnum.Passed);
            predicate = predicate.And(m => m.ArticleType == ArticleTypeEnum.Article);
            // 关键词检索不再放进表达式：正文命中需要 CONTAINS / MATCH...AGAINST，表达式无法表达，
            // 统一由 BuildArticleSearchCondition 按数据库能力拼 SQL（见方法注释）。
            predicate = predicate.AndIF(parm.TopicId != null, m => m.TopicId == parm.TopicId);

            if (parm.CategoryId != null)
            {
                var allChildCategory = Context.Queryable<ArticleCategory>()
                    .ToChildList(m => m.ParentId, parm.CategoryId);
                var categoryIdList = allChildCategory.Select(x => x.CategoryId).ToArray();
                predicate = predicate.And(m => categoryIdList.Contains(m.CategoryId));
            }

            var keyword = parm.SearchText?.Trim();
            var (searchSql, searchParams) = BuildArticleSearchCondition(keyword);

            var response = Queryable()
                // 有关键词时不走缓存：关键词组合无穷，缓存只会持续膨胀；且全文检索结果需要即时可见
                .WithCacheIF(string.IsNullOrEmpty(keyword), 60 * 30)
                .Includes(x => x.CategoryNav)
                .LeftJoin<SysUser>((m, u) => m.UserId == u.UserId).Filter(null, true)
                .Where(predicate.ToExpression())
                // 关键词条件：标题/摘要 LIKE + 正文按数据库能力（全文检索 / LIKE），见 BuildArticleSearchCondition
                .WhereIF(!string.IsNullOrEmpty(searchSql), searchSql, searchParams)
                .OrderByDescending(m => m.Cid)
                .Select((m, u) => new ArticleDto()
                {
                    User = new ArticleUser()
                    {
                        Avatar = u.Avatar,
                        NickName = u.NickName,
                        Sex = u.Sex,
                    },
                    Content = string.Empty,
                    UserIP = string.Empty
                }, true)
                .ToPage(parm);

            if (parm.UserId > 0)
            {
                Context.ThenMapper(response.Result, item =>
                {
                    item.IsPraise = Context.Queryable<ArticlePraise>()
                    .Where(f => f.UserId == parm.UserId && f.IsDelete == 0)
                    .SetContext(scl => scl.ArticleId, () => item.Cid, item).Any() ? 1 : 0;
                });
            }

            return response;
        }

        /// <summary>
        /// 构造前台关键词检索条件（SQL 片段 + 参数），keyword 为空时返回 (null, null) 表示不加条件。
        /// 标题/摘要固定用 LIKE（MySQL、SqlServer 都支持）；
        /// 正文在"已建全文索引 + MySQL/SqlServer"时走全文检索（SQL Server 用 CONTAINS，MySQL 用 MATCH...AGAINST），
        /// 其余情况（未建索引、其他数据库、探测失败）统一退化为 LIKE，保证任意库上接口都可用。
        /// 注意：MySQL 的 ngram 分词器默认 token 长度为 2，单字关键词不会命中正文，属预期行为。
        /// </summary>
        private (string Sql, object Parameters) BuildArticleSearchCondition(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return (null, null);

            var kwLike = $"%{keyword}%";

            if (IsArticleContentFullTextReady())
            {
                var dbType = Context.CurrentConnectionConfig.DbType;
                if (dbType == DbType.SqlServer)
                {
                    // 对应 CREATE FULLTEXT INDEX ON article([Content])，见 doc/文章正文全文检索改造.md
                    return ("(Title LIKE @kwLike OR AbstractText LIKE @kwLike OR CONTAINS(Content, @kw))",
                        new { kwLike, kw = keyword });
                }
                if (dbType == DbType.MySql)
                {
                    // 对应 ALTER TABLE article ADD FULLTEXT INDEX ... WITH PARSER ngram
                    return ("(Title LIKE @kwLike OR AbstractText LIKE @kwLike OR MATCH(Content) AGAINST (@kw))",
                        new { kwLike, kw = keyword });
                }
            }

            return ("(Title LIKE @kwLike OR AbstractText LIKE @kwLike OR Content LIKE @kwLike)", new { kwLike });
        }

        /// <summary>
        /// 文章正文全文索引是否就绪：SQL Server 查 sys.fulltext_indexes，MySQL 查 information_schema.STATISTICS。
        /// 结果缓存 10 分钟，避免每次搜索都探测；索引属部署动作，建好后最多 10 分钟自动生效，无需重启。
        /// 探测失败一律按"未就绪"处理，让查询退化为 LIKE，不能因为索引缺失让搜索不可用。
        /// </summary>
        private bool IsArticleContentFullTextReady()
        {
            string configId = Context.CurrentConnectionConfig.ConfigId?.ToString();
            string cacheKey = $"ARTICLE_CONTENT_FTS_READY_{configId}";
            if (CacheHelper.Exists(cacheKey))
            {
                return CacheHelper.GetCache(cacheKey)?.ToString() == "1";
            }

            var ready = false;
            try
            {
                var dbType = Context.CurrentConnectionConfig.DbType;
                if (dbType == DbType.SqlServer)
                {
                    ready = Context.Ado.GetInt(
                        "SELECT COUNT(1) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('article')") > 0;
                }
                else if (dbType == DbType.MySql)
                {
                    ready = Context.Ado.GetInt(
                        "SELECT COUNT(1) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'article' AND INDEX_NAME = @indexName",
                        new { indexName = ArticleContentFtsIndex }) > 0;
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"检测文章正文全文索引失败，本次按 LIKE 检索：{ex.Message}");
            }

            CacheHelper.SetCache(cacheKey, ready ? "1" : "0", 10);
            return ready;
        }

        /// <summary>
        /// 查询最新文章列表
        /// </summary>
        /// <returns></returns>
        public List<ArticleDto> GetNewArticleList()
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.And(m => m.ArticleType == ArticleTypeEnum.Article);
            predicate = predicate.And(m => m.AuditStatus == AuditStatusEnum.Passed);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Includes(x => x.CategoryNav) //填充子对象
                .Take(10)
                .OrderBy(f => f.CreateTime, OrderByType.Desc)
                .ToList();
            return response.Adapt<List<ArticleDto>>();
        }

        /// <summary>
        /// 前台查询动态列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetMonentList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.And(m => m.ArticleType == ArticleTypeEnum.Monent);
            predicate = predicate.And(m => m.AuditStatus == AuditStatusEnum.Passed);
            predicate = predicate.AndIF(parm.TopicId != null, m => m.TopicId == parm.TopicId);
            predicate = predicate.AndIF(parm.CategoryId != null, m => m.CategoryId == parm.CategoryId);

            //获取我的圈子
            if (parm.QueryMyJoin)
            {
                var myJoin = Context.Queryable<ArticleUserCircles>()
                    .Where(m => m.UserId == parm.UserId)
                    .Select(m => m.CategoryId)
                    .ToList();
                predicate = predicate.And(m => myJoin.Contains(m.CategoryId));
            }
            var response = Queryable()
                .Includes(x => x.CategoryNav) //填充子对象
                .LeftJoin<SysUser>((m, u) => m.UserId == u.UserId).Filter(null, true)
                .Where(predicate.ToExpression())
                .OrderByIF(parm.OrderBy == 1, m => new { m.PraiseNum, m.CommentNum }, OrderByType.Desc)
                .OrderByIF(parm.OrderBy == 2, m => new { m.Cid }, OrderByType.Desc)
                .OrderBy(m => m.Cid, OrderByType.Desc)
                .Select((m, u) => new ArticleDto()
                {
                    User = new ArticleUser()
                    {
                        Avatar = u.Avatar,
                        NickName = u.NickName,
                        Sex = u.Sex,
                    },
                }, true)
                .ToPage(parm);

            if (parm.UserId > 0)
            {
                Context.ThenMapper(response.Result, item =>
                {
                    item.IsPraise = Context.Queryable<ArticlePraise>()
                    .Where(f => f.UserId == parm.UserId && f.IsDelete == 0)
                    .SetContext(scl => scl.ArticleId, () => item.Cid, item).Any() ? 1 : 0;
                });
            }

            return response;
        }

        /// <summary>
        /// 查询圈子内容列表（文章+动态混合，按分类含子分类）
        /// 用于圈子详情页，统一返回该圈子下所有公开且审核通过的内容，
        /// 不限制内容类型，并补全点赞/评论数及当前用户是否点赞。
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetCircleList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();
            predicate = predicate.And(m => m.Status == "1");
            predicate = predicate.And(m => m.IsPublic == 1);
            predicate = predicate.And(m => m.AuditStatus == AuditStatusEnum.Passed);
            predicate = predicate.AndIF(parm.TopicId != null, m => m.TopicId == parm.TopicId);

            if (parm.CategoryId != null)
            {
                var allChildCategory = Context.Queryable<ArticleCategory>()
                    .ToChildList(m => m.ParentId, parm.CategoryId);
                var categoryIdList = allChildCategory.Select(x => x.CategoryId).ToArray();
                predicate = predicate.And(m => categoryIdList.Contains(m.CategoryId));
            }

            var response = Queryable()
                .Includes(x => x.CategoryNav)
                .LeftJoin<SysUser>((m, u) => m.UserId == u.UserId).Filter(null, true)
                .Where(predicate.ToExpression())
                .OrderByIF(parm.OrderBy == 1, m => new { m.PraiseNum, m.CommentNum }, OrderByType.Desc)
                .OrderByIF(parm.OrderBy == 2, m => m.Cid, OrderByType.Desc)
                .OrderBy(m => m.Cid, OrderByType.Desc)
                .Select((m, u) => new ArticleDto()
                {
                    User = new ArticleUser()
                    {
                        Avatar = u.Avatar,
                        NickName = u.NickName,
                        Sex = u.Sex,
                    },
                }, true)
                .ToPage(parm);

            if (parm.UserId > 0)
            {
                Context.ThenMapper(response.Result, item =>
                {
                    item.IsPraise = Context.Queryable<ArticlePraise>()
                    .Where(f => f.UserId == parm.UserId && f.IsDelete == 0)
                    .SetContext(scl => scl.ArticleId, () => item.Cid, item).Any() ? 1 : 0;
                });
            }

            return response;
        }

        /// <summary>
        /// 查询我的文章列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ArticleDto> GetMyList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Status), m => m.Status == parm.Status);
            predicate = predicate.AndIF(parm.BeginTime != null, m => m.CreateTime >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime != null, m => m.CreateTime <= parm.EndTime);
            predicate = predicate.And(m => m.UserId == parm.UserId);
            predicate = predicate.AndIF(parm.ArticleType != null, m => (int)m.ArticleType == parm.ArticleType);
            predicate = predicate.AndIF(parm.TabId == 2, m => m.IsPublic == 0 && m.UserId == parm.UserId);

            var response = Queryable()
                //.IgnoreColumns(x => new { x.Content })
                .Includes(x => x.CategoryNav)
                .Where(predicate.ToExpression())
                .OrderByIF(parm.TabId == 3, m => new { m.IsTop, m.PraiseNum }, OrderByType.Desc)
                .OrderByDescending(m => new { m.IsTop, m.Cid })
                .Select((x) => new ArticleDto()
                {
                    Content = x.ArticleType == 0 ? string.Empty : x.Content,
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 修改文章管理
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateArticle(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                Title = model.Title,
                Content = model.Content,
                Status = model.Status,
                Tags = model.Tags,
                UpdateTime = DateTime.Now,
                CoverUrl = model.CoverUrl,
                CategoryId = model.CategoryId,
                IsPublic = model.IsPublic,
                AbstractText = model.AbstractText,
            });
            return response;
        }

        /// <summary>
        /// 置顶文章
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int TopArticle(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                IsTop = model.IsTop,
            });
            return response;
        }

        /// <summary>
        /// 评论权限
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int ChangeComment(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                CommentSwitch = model.CommentSwitch,
            });
            return response;
        }

        /// <summary>
        /// 是否公开
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int ChangeArticlePublic(Article model)
        {
            var response = Update(w => w.Cid == model.Cid, it => new Article()
            {
                IsPublic = model.IsPublic,
            });
            return response;
        }

        /// <summary>
        /// 修改文章访问量
        /// </summary>
        /// <param name="cid"></param>
        /// <returns></returns>
        public int UpdateArticleHit(long cid)
        {
            var response = Update(w => w.Cid == cid, it => new Article() { Hits = it.Hits + 1 });
            return response;
        }

        /// <summary>
        /// 点赞
        /// </summary>
        /// <param name="cid"></param>
        /// <returns></returns>
        public int PraiseArticle(long cid)
        {
            return Update(w => w.Cid == cid, it => new Article() { PraiseNum = it.PraiseNum + 1 });
        }
        public int CancelPraise(long cid)
        {
            return Update(w => w.Cid == cid, it => new Article() { PraiseNum = it.PraiseNum - 1 });
        }

        public Article PublishArticle(Article article)
        {
            return Publish(article);
        }

        /// <summary>
        /// 前端-发布动态,文章
        /// </summary>
        /// <param name="article"></param>
        /// <returns></returns>
        public Article Publish(Article article)
        {
            var httpContext = App.HttpContext;
            article.AuthorName = HttpContextExtension.GetName(httpContext);
            article.UserId = HttpContextExtension.GetUId(httpContext);
            article.UserIP = HttpContextExtension.GetClientUserIp(httpContext);
            article.Location = HttpContextExtension.GetIpInfo(article.UserIP);
            article.AuditStatus = AuditStatusEnum.Passed;
            // 列表页依赖 abstractText 展示摘要，前端不传时按正文自动生成，避免列表摘要为空
            article.AbstractText = BuildAbstractText(article.Content, article.AbstractText);

            article = InsertReturnEntity(article);
            if (article.Status == "1")
            {
                //跟新话题加入数
                if (article.Cid > 0 && article.TopicId > 0)
                {
                    _topicService.Update(w => w.TopicId == article.TopicId, it => new ArticleTopic() { JoinNum = it.JoinNum + 1 });
                }
                //更新发布文章数
                if (article.Cid > 0 && article.CategoryId > 0)
                {
                    _categoryService.Update(w => w.CategoryId == article.CategoryId, it => new ArticleCategory() { ArticleNum = it.ArticleNum + 1 });
                }
            }
            return article;
        }

        /// <summary>
        /// 获取文章详情
        /// </summary>
        /// <param name="cid">内容id</param>
        /// <param name="userId">当前用户id</param>
        /// <returns></returns>
        /// <exception cref="CustomException"></exception>
        public ArticleDto GetArticle(long cid, long userId)
        {
            var response = GetId(cid);
            var model = response.Adapt<ArticleDto>() ?? throw new CustomException(ResultCode.FAIL, "内容不存在");
            if (model.IsPublic == 0 && userId != model.UserId)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "访问失败");
            }
            if (model != null)
            {
                model.CategoryNav = _categoryService.GetById(model.CategoryId).Adapt<ArticleCategoryDto>();
            }

            var webContext = App.HttpContext;
            var CK = "ARTICLE_DETAILS_" + userId + HttpContextExtension.GetClientUserIp(webContext);
            if (!CacheHelper.Exists(CK))
            {
                UpdateArticleHit(cid);
                var userIP = HttpContextExtension.GetClientUserIp(webContext);
                var location = HttpContextExtension.GetIpInfo(userIP);
                itenant.InsertableWithAttr(new ArticleBrowsingLog()
                {
                    ArticleId = cid,
                    UserId = userId,
                    Location = location,
                    UserIP = userIP,
                    AddTime = DateTime.Now,
                }).ExecuteReturnSnowflakeId();
            }
            CacheHelper.SetCache(CK, 1, 10);
            if (userId > 0)
            {
                model.IsPraise = Context.Queryable<ArticlePraise>()
               .Where(f => f.UserId == userId && f.ArticleId == cid && f.IsDelete == 0)
               .Any() ? 1 : 0;
            }

            return model;
        }

        /// <summary>
        /// 审核通过
        /// </summary>
        /// <param name="idsArr"></param>
        /// <returns></returns>
        public int Passed(long[] idsArr)
        {
            int result = 0;
            List<Article> monents = [];
            foreach (var item in idsArr)
            {
                var model = GetFirst(x => x.Cid == item && x.AuditStatus == 0);
                if (model == null) continue;

                //model.Auditor = HttpContext.GetName();
                model.AuditStatus = AuditStatusEnum.Passed;
                var response = Update(w => w.Cid == model.Cid, it => new Article()
                {
                    AuditStatus = model.AuditStatus,
                    //Auditor = model.Auditor,
                });
                result += response;
                monents.Add(model);
            }
            var data = from a in monents
                       group a by new { a.UserId } into grp
                       select new
                       {
                           userid = grp.Key.UserId,
                           num = grp.Count()
                       };
            foreach (var item in data)
            {
                _userMsgService.AddSysUserMsg(item.userid, "您发布的内容已通过审核", UserMsgType.SYSTEM);
            }

            return result;
        }

        /// <summary>
        /// 审核不通过
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="idsArr"></param>
        /// <returns></returns>
        public int Reject(string reason, long[] idsArr)
        {
            int result = 0;
            List<Article> monents = new();
            foreach (var item in idsArr)
            {
                var model = GetFirst(x => x.Cid == item && x.AuditStatus == 0);
                if (model == null) continue;

                //model.Auditor = HttpContext.GetName();
                model.AuditStatus = AuditStatusEnum.Reject;
                var response = Update(w => w.Cid == model.Cid, it => new Article()
                {
                    AuditStatus = model.AuditStatus,
                    //Auditor = model.Auditor,
                });
                result += response;
                monents.Add(model);
            }
            var data = from a in monents
                       group a by new { a.UserId } into grp
                       select new
                       {
                           userid = grp.Key.UserId,
                           num = grp.Count()
                       };
            foreach (var item in data)
            {
                //Console.WriteLine(item.useridx +"," + item.num);
                string content = $"您发布的内容未通过审核。";
                if (!string.IsNullOrEmpty(reason))
                {
                    content += $"原因：{reason}";
                }
                _userMsgService.AddSysUserMsg(item.userid, content, UserMsgType.SYSTEM);
            }

            return result;
        }

        /// <summary>
        /// 导出文章列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public List<ArticleExportDto> ExportList(ArticleQueryDto parm)
        {
            var predicate = Expressionable.Create<Article>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), m => m.Title.Contains(parm.Title));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.AbstractText), m => m.AbstractText.Contains(parm.AbstractText));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Status), m => m.Status == parm.Status);
            predicate = predicate.AndIF(parm.IsPublic != null, m => m.IsPublic == parm.IsPublic);
            predicate = predicate.AndIF(parm.IsTop != null, m => m.IsTop == parm.IsTop);
            predicate = predicate.AndIF(parm.ArticleType != null, m => (int)m.ArticleType == parm.ArticleType);
            predicate = predicate.AndIF(parm.TopicId != null, m => m.TopicId == parm.TopicId);
            predicate = predicate.AndIF(parm.AuditStatus != null, m => m.AuditStatus == parm.AuditStatus);
            predicate = predicate.AndIF(parm.BeginTime != null, m => m.CreateTime >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime != null, m => m.CreateTime <= parm.EndTime);

            if (parm.CategoryId != null)
            {
                var allChildCategory = Context.Queryable<ArticleCategory>()
                    .ToChildList(m => m.ParentId, parm.CategoryId);
                var categoryIdList = allChildCategory.Select(x => x.CategoryId).ToArray();
                predicate = predicate.And(m => categoryIdList.Contains(m.CategoryId));
            }

            var list = Queryable()
                .Includes(x => x.CategoryNav)
                .Where(predicate.ToExpression())
                .OrderByDescending(m => m.Cid)
                .ToList();

            return list.Select(x => new ArticleExportDto
            {
                Cid = x.Cid,
                Title = x.Title,
                Content = x.Content,
                AuthorName = x.AuthorName,
                Status = x.Status,
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryNav?.Name,
                Tags = x.Tags,
                IsPublic = x.IsPublic,
                IsTop = x.IsTop,
                ArticleType = x.ArticleType,
                AuditStatus = x.AuditStatus,
                CreateTime = x.CreateTime
            }).ToList();
        }

        /// <summary>
        /// 导入文章
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public int ImportArticle(List<ArticleImportDto> list)
        {
            if (list == null || list.Count <= 0)
            {
                return 0;
            }

            int successCount = 0;
            foreach (var item in list)
            {
                if (item.Title.IsEmpty() || item.Content.IsEmpty())
                {
                    continue;
                }

                var entity = new Article
                {
                    Title = item.Title,
                    Content = item.Content,
                    Status = "2",
                    CategoryId = 0,
                    IsPublic = item.IsPublic == "是" ? 1 : 0,
                    ArticleType = item.ArticleType.HasValue ? (ArticleTypeEnum)item.ArticleType.Value : ArticleTypeEnum.Article,
                    AbstractText = item.AbstractText,
                    CommentSwitch = CommentSwitchEnum.ALL,
                    CreateTime = DateTime.Now,
                    EditorType = item.EditorType ?? "html",
                };

                Publish(entity);
                successCount++;
            }

            return successCount;
        }

        /// <summary>
        /// 前端-编辑我的内容（仅作者本人可操作）
        /// 安全与语义约定：
        /// 1. 归属由服务端校验（old.UserId == userId），且不修改 UserId / ArticleType，
        ///    避免前端越权改归属或把动态改成文章；
        /// 2. 只更新内容相关字段，Hits / PraiseNum / CommentNum 由各自行为接口维护，编辑不覆盖；
        /// 3. AuditStatus 保持原值（当前发布即过审，编辑不把已通过的内容打回待审）；
        /// 4. 字段值 null = 未提交，保留原值；空串 = 显式清空（封面、标签可据此清掉），
        ///    避免部分表单把没提交的字段清空造成数据丢失；
        /// 5. Status / CategoryId 为 0 或空时视为"不修改"。
        /// </summary>
        public int UpdateMyArticle(Article model, long userId)
        {
            if (model == null || model.Cid <= 0)
            {
                throw new CustomException(ResultCode.PARAM_ERROR, "缺少内容Id");
            }

            var old = GetFirst(x => x.Cid == model.Cid);
            if (old == null) throw new CustomException(ResultCode.CUSTOM_ERROR, "内容不存在");
            if (old.UserId != userId) throw new CustomException(ResultCode.DENY, "无权编辑他人内容");

            var newTitle = model.Title ?? old.Title;
            var newContent = model.Content ?? old.Content;
            if (string.IsNullOrWhiteSpace(newTitle)) throw new CustomException(ResultCode.PARAM_ERROR, "标题不能为空");
            if (string.IsNullOrWhiteSpace(newContent)) throw new CustomException(ResultCode.PARAM_ERROR, "内容不能为空");

            // 摘要：调用方传了就用调用方的；没传则按（新的）正文重新生成
            var newAbstract = BuildAbstractText(newContent, model.AbstractText);
            var newStatus = string.IsNullOrEmpty(model.Status) ? old.Status : model.Status;
            var newCategoryId = model.CategoryId > 0 ? model.CategoryId : old.CategoryId;

            var response = Update(w => w.Cid == old.Cid, it => new Article()
            {
                Title = newTitle.Trim(),
                Content = newContent,
                AbstractText = newAbstract,
                Status = newStatus,
                Tags = model.Tags ?? old.Tags,
                CoverUrl = model.CoverUrl ?? old.CoverUrl,
                CategoryId = newCategoryId,
                IsPublic = model.IsPublic,
                UpdateTime = DateTime.Now,
            });

            // 与 Publish 对齐：草稿/下线内容经编辑首次发布时补一次话题、分类计数。
            // 反向（发布→草稿/下线）不递减：历史数据不一定统计过，减下去会出现负数。
            if (response > 0 && old.Status != "1" && newStatus == "1")
            {
                if (old.TopicId > 0)
                {
                    _topicService.Update(w => w.TopicId == old.TopicId, it => new ArticleTopic() { JoinNum = it.JoinNum + 1 });
                }
                if (newCategoryId > 0)
                {
                    _categoryService.Update(w => w.CategoryId == newCategoryId, it => new ArticleCategory() { ArticleNum = it.ArticleNum + 1 });
                }
            }

            return response;
        }

        /// <summary>
        /// 我的创作统计：一次聚合查询完成，不在内存里遍历明细。
        /// 统计范围是 article 表中当前用户的全部内容（文章 + 动态 + 随笔）。
        /// </summary>
        public MyArticleStatsDto GetMyStats(long userId)
        {
            if (userId <= 0) throw new CustomException(ResultCode.PARAM_ERROR, "用户信息无效");

            var stats = Queryable()
                .Where(x => x.UserId == userId)
                .Select(x => new MyArticleStatsDto
                {
                    Total = SqlFunc.AggregateCount(x.Cid),
                    Published = SqlFunc.AggregateSum(x.Status == "1" ? 1 : 0),
                    Draft = SqlFunc.AggregateSum(x.Status == "2" ? 1 : 0),
                    Offline = SqlFunc.AggregateSum(x.Status == "0" ? 1 : 0),
                    Private = SqlFunc.AggregateSum(x.IsPublic == 0 ? 1 : 0),
                    Pending = SqlFunc.AggregateSum((int)x.AuditStatus == (int)AuditStatusEnum.Review ? 1 : 0),
                    Rejected = SqlFunc.AggregateSum((int)x.AuditStatus == (int)AuditStatusEnum.Reject ? 1 : 0),
                    Hits = SqlFunc.IsNull(SqlFunc.AggregateSum(x.Hits), 0),
                    PraiseNum = SqlFunc.IsNull(SqlFunc.AggregateSum(x.PraiseNum), 0),
                    CommentNum = SqlFunc.IsNull(SqlFunc.AggregateSum(x.CommentNum), 0),
                })
                .First();

            return stats ?? new MyArticleStatsDto();
        }

        /// <summary>
        /// 单篇内容统计：公开内容任何人可看；非公开内容仅作者本人可看（他人直接返回"内容不存在"，不泄露存在性）。
        /// 与详情接口不同，本接口只读，不会自增浏览量、不写浏览日志。
        /// </summary>
        public ArticleStatsDto GetArticleStats(long cid, long userId)
        {
            if (cid <= 0) throw new CustomException(ResultCode.PARAM_ERROR, "缺少内容Id");

            var model = Queryable()
                .Where(x => x.Cid == cid && (x.IsPublic == 1 || x.UserId == userId))
                .Select(x => new ArticleStatsDto
                {
                    Cid = x.Cid,
                    Title = x.Title,
                    Hits = x.Hits,
                    PraiseNum = x.PraiseNum,
                    CommentNum = x.CommentNum,
                    IsPublic = x.IsPublic,
                    Status = x.Status,
                    AuditStatus = x.AuditStatus,
                    CreateTime = x.CreateTime,
                    UpdateTime = x.UpdateTime,
                })
                .First();

            return model ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "内容不存在");
        }

        /// <summary>
        /// 站点内容统计（前台展示用）：只统计"已发布 + 公开 + 审核通过"的文章。
        /// 结果按库（ConfigId）缓存 10 分钟，避免每次访问都做全表聚合；
        /// 缓存 key 带 ConfigId，多租户环境各库互不串数据。
        /// </summary>
        public SiteArticleStatsDto GetSiteStats()
        {
            string configId = Context.CurrentConnectionConfig.ConfigId?.ToString();
            string cacheKey = $"ARTICLE_SITE_STATS_{configId}";

            if (CacheHelper.GetCache(cacheKey) is SiteArticleStatsDto cached) return cached;

            var stats = Queryable()
                .Where(x => x.Status == "1"
                    && x.IsPublic == 1
                    && x.AuditStatus == AuditStatusEnum.Passed
                    && x.ArticleType == ArticleTypeEnum.Article)
                .Select(x => new SiteArticleStatsDto
                {
                    ArticleCount = SqlFunc.AggregateCount(x.Cid),
                    TodayCount = SqlFunc.AggregateSum(x.CreateTime >= DateTime.Today ? 1 : 0),
                    Hits = SqlFunc.IsNull(SqlFunc.AggregateSum(x.Hits), 0),
                    PraiseNum = SqlFunc.IsNull(SqlFunc.AggregateSum(x.PraiseNum), 0),
                    CommentNum = SqlFunc.IsNull(SqlFunc.AggregateSum(x.CommentNum), 0),
                })
                .First() ?? new SiteArticleStatsDto();

            CacheHelper.SetCache(cacheKey, stats, 10);
            return stats;
        }

        /// <summary>
        /// 前端-编辑前取数：返回自己的内容明细（含正文）。
        /// 与详情接口不同：不做浏览量自增、不写浏览日志，避免作者每次打开编辑页都把阅读数刷上去。
        /// </summary>
        public ArticleDto GetMyArticleForEdit(long cid, long userId)
        {
            if (cid <= 0) throw new CustomException(ResultCode.PARAM_ERROR, "缺少内容Id");

            var entity = GetFirst(x => x.Cid == cid);
            if (entity == null) throw new CustomException(ResultCode.CUSTOM_ERROR, "内容不存在");
            if (entity.UserId != userId) throw new CustomException(ResultCode.DENY, "无权查看他人内容");

            return entity.Adapt<ArticleDto>();
        }

        /// <summary>
        /// 生成列表页摘要：调用方已填则按上限截断；未填则从正文（HTML / Markdown）提取纯文本生成。
        /// 前台列表接口依赖 AbstractText 展示摘要，发布/编辑时不传摘要不能留空。
        /// </summary>
        /// <param name="content">正文原始内容（可能含 HTML 标签或 Markdown 标记）</param>
        /// <param name="abstractText">调用方传入的摘要，非空时优先使用</param>
        /// <param name="maxLength">摘要最大长度</param>
        private static string BuildAbstractText(string content, string abstractText, int maxLength = 100)
        {
            if (!string.IsNullOrWhiteSpace(abstractText))
            {
                var provided = abstractText.Trim();
                return provided.Length > maxLength ? provided[..maxLength] : provided;
            }
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;

            // 去 HTML 标签 → 反转义实体（&nbsp; 等）→ 去常见 Markdown 标记 → 折叠空白
            var text = Regex.Replace(content, "<[^>]*>", " ");
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"[#>*`~\[\]\(\)!|_]+", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text.Length > maxLength ? text[..maxLength] + "…" : text;
        }
    }
}
