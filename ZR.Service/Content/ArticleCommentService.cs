using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using ZR.Model.Content;
using ZR.Model.Content.Dto;
using ZR.Model.System;
using ZR.Repository;
using ZR.Service.Content.IService;
using ZR.ServiceCore.Services;

namespace ZR.Service.Content
{
    [AppService(ServiceType = typeof(IArticleCommentService), ServiceLifetime = LifeTime.Transient)]
    public class ArticleCommentService : BaseService<ArticleComment>, IArticleCommentService
    {
        private ISysUserService UserService;
        private IEmailLogService EmailLogService;
        private IArticleService ArticleService;
        private ISysConfigService SysConfigService;
        private ISysUserMsgService UserMsgService;
        public ArticleCommentService(
            ISysUserService userService,
            IEmailLogService emailLogService,
            IArticleService articleService,
            ISysConfigService sysConfigService,
            ISysUserMsgService sysUserMsgService)
        {
            this.UserService = userService;
            EmailLogService = emailLogService;
            ArticleService = articleService;
            SysConfigService = sysConfigService;
            UserMsgService = sysUserMsgService;
        }

        /// <summary>
        /// 获取评论列表
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public PagedInfo<ArticleCommentDto> GetMessageList(MessageQueryDto dto)
        {
            var predicate = Expressionable.Create<ArticleComment>();
            predicate.And(it => it.IsDelete == 0);
            predicate.And(it => it.ParentId == dto.CommentId);
            predicate.AndIF(dto.UserId != null, it => it.UserId == dto.UserId);
            predicate.AndIF(dto.CommentId > 0, it => it.CommentId > dto.CommentId);//分页使用
            //predicate.AndIF(dto.ClassifyId > 0, it => it.ClassifyId == dto.ClassifyId);
            //predicate.AndIF(dto.ClassifyId == 0, it => it.ClassifyId == 0);
            predicate.AndIF(dto.TargetId > 0, it => it.TargetId == dto.TargetId);

            var list = Queryable()
                .LeftJoin<SysUser>((it, u) => it.UserId == u.UserId)
                .OrderByDescending(it => it.Top)
                .OrderByIF(dto.OrderBy == 1, it => it.PraiseNum, OrderByType.Desc)
                .OrderByIF(dto.OrderBy == 2, it => it.CommentId, OrderByType.Desc)
                .Where(predicate.ToExpression())
                .WithCache(60 * 30)
                .Select((it, u) => new ArticleCommentDto()
                {
                    NickName = u.NickName,
                    Avatar = u.Avatar
                }, true)
                .ToPage(dto);

            foreach (var item in list.Result)
            {
                int take = 1;
                if (item.ReplyNum > 0)
                {
                    item.ReplyList = GetCommentQueryable(item.CommentId).Take(take).ToList();
                }
                if (item.ReplyNum > take)
                {
                    item.HasMore = true;
                }
            }

            return list;
        }

        /// <summary>
        /// 获取评论回复列表
        /// </summary>
        /// <param name="pager">分页</param>
        /// <param name="cid"></param>
        /// <returns></returns>
        public PagedInfo<ArticleCommentDto> GetReplyComments(long cid, MessageQueryDto pager)
        {
            PagedInfo<ArticleCommentDto> list = GetCommentQueryable(cid).ToPage(pager);
            //Context.ThenMapper(list.Result, f =>
            //{
            //    if (f.ReplyId > 0 && f.ReplyUserId > 0)
            //    {
            //        f.ReplyNickName = UserService.GetFirst(x => x.UserId == f.ReplyUserId).NickName;
            //    }
            //});
            return list;
        }

        private ISugarQueryable<ArticleCommentDto> GetCommentQueryable(long cid)
        {
            return Queryable()
                            .LeftJoin<SysUser>((f, u) => f.UserId == u.UserId)
                            .Where(f => f.ParentId == cid && f.IsDelete == 0)
                            //.WhereIF(cid > 0, f => f.MId > cid)
                            //.Includes(f => f.User.MappingField(z => z.Useridx, () => f.Useridx))
                            .OrderBy(f => f.CommentId)
                            .Select((f, u) => new ArticleCommentDto()
                            {
                                NickName = u.NickName,
                                Avatar = u.Avatar
                            }, true);
        }

        /// <summary>
        /// 评论
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public ArticleComment AddMessage(ArticleComment message)
        {
            var configInfo = SysConfigService.GetFirst(f => f.ConfigKey == "article.comment") ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "评论失败，请联系管理员");
            var userInfo = UserService.GetById(message.UserId);

            if (configInfo.ConfigValue == "1" && userInfo.Phonenumber.IsEmpty())
            {
                throw new CustomException(ResultCode.PHONE_BIND, "请绑定手机号");
            }
            var contentInfo = ArticleService.GetById(message.TargetId);
            switch (contentInfo.CommentSwitch)
            {
                case Model.Enum.CommentSwitchEnum.ALL:
                    break;
                case Model.Enum.CommentSwitchEnum.FANS:
                    break;
                case Model.Enum.CommentSwitchEnum.SELF:
                    if (message.UserId != contentInfo.UserId)
                    {
                        throw new CustomException("仅作者才能评论");
                    }
                    break;
                default:
                    break;
            }
            var ipInfo = HttpContextExtension.GetIpInfo(message.UserIP);
            message.Location = ipInfo;
            message.AddTime = DateTime.Now;
            ArticleComment result = null;
            var r = UseTran(() =>
            {
                result = InsertReturnEntity(message);
                if (result != null && result.CommentId > 0)
                {
                    if (message.ParentId > 0)
                    {
                        //评论表 评论数 + 1
                        Update(it => it.CommentId == message.ParentId, it => new ArticleComment() { ReplyNum = it.ReplyNum + 1 });
                    }

                    //内容表评论数加1
                    if (message.TargetId > 0)
                    {
                        ArticleService.Update(it => it.Cid == (int)message.TargetId, it => new Article() { CommentNum = it.CommentNum + 1 });
                    }
                }
            });
            //查询出评论的内容信息找出作者
            var targetInfo = GetFirst(f => f.CommentId == message.TargetId);
            if (targetInfo != null && targetInfo.UserId != message.UserId)
            {
                //IM消息
                UserMsgService.AddSysUserMsg(targetInfo.UserId, message.Content, UserMsgType.COMMENT);
            }
            if (message.UserId != contentInfo?.UserId)
            {
                //给作者发送IM消息
                UserMsgService.AddSysUserMsg(contentInfo.UserId, message.Content, UserMsgType.COMMENT);
            }
            
            return result;
        }

        /// <summary>
        /// 评论点赞
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public int PraiseMessage(long mid)
        {
            return Context.Updateable<ArticleComment>()
                .SetColumns(it => it.PraiseNum == it.PraiseNum + 1)
                .Where(it => it.CommentId == mid)
                .ExecuteCommand();
        }

        /// <summary>
        /// 删除评论
        /// </summary>
        /// <param name="commentId">评论ID</param>
        /// <param name="userId">当前登录用户</param>
        /// <returns></returns>
        public int DeleteMessage(long commentId, long userId)
        {
            var info = GetById(commentId) ?? throw new CustomException("评论不存在");
            var postInfo = ArticleService.GetById(info.TargetId);
            if (userId != info.UserId && userId != postInfo.UserId)
            {
                return 0;
            }
            var deleteNum = 0;
            var result = UseTran(() =>
            {
                Update(it => it.CommentId == commentId, it => new ArticleComment() { IsDelete = 1 });
                if (info.ParentId > 0)
                {
                    //评论表 评论数 - 1
                    Update(it => it.CommentId == info.ParentId, it => new ArticleComment() { ReplyNum = it.ReplyNum - 1 });
                }
                //减少文章评论次数
                if (info.TargetId > 0)
                {
                    deleteNum = info.ReplyNum > 0 ? info.ReplyNum + 1 : 1;

                    ArticleService.Update(it => it.Cid == (int)info.TargetId, it => new Article()
                    {
                        CommentNum = it.CommentNum - deleteNum
                    });
                }
            });
            return result.IsSuccess ? deleteNum : 0;
        }

        /// <summary>
        /// 获取评论列表
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public PagedInfo<ArticleCommentDto> GetMyMessageList(MessageQueryDto dto)
        {
            var predicate = Expressionable.Create<ArticleComment>();
            predicate.And(it => it.IsDelete == 0);
            //predicate.And(it => it.ParentId == dto.MId);
            predicate.AndIF(dto.UserId != null, it => it.UserId == dto.UserId);
            predicate.AndIF(dto.CommentId > 0, it => it.CommentId > dto.CommentId);//分页使用
            predicate.AndIF(dto.BeginAddTime != null, it => it.AddTime >= dto.BeginAddTime && it.AddTime <= dto.EndAddTime);//分页使用

            return Queryable()
                .WithCache(60)
                .OrderByIF(dto.OrderBy == 1, it => it.PraiseNum, OrderByType.Desc)
                .OrderByIF(dto.OrderBy == 2, it => it.CommentId, OrderByType.Desc)
                .Where(predicate.ToExpression())
                //.Select((it, u) => new MessageDto()
                //{
                //    MId = it.MId.SelectAll(),
                //    //NickName = u.NickName,
                //    //Avatar = u.Avatar
                //}, true)
                .ToPage<ArticleComment, ArticleCommentDto>(dto);
        }

        /// <summary>
        /// 置顶评论
        /// </summary>
        /// <param name="commentId"></param>
        /// <param name="top"></param>
        /// <returns></returns>
        public long TopMessage(long commentId, long top)
        {
            long time = 0;
            if (top <= 0)
            {
                time = DateTimeHelper.GetUnixTimeStamp(DateTime.Now);
            }
            Update(it => it.CommentId == commentId, it => new ArticleComment() { Top = time });
            return time;
        }
    }
}
