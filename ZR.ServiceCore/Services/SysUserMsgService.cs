using Infrastructure;
using Infrastructure.Attribute;
using Mapster;
using ZR.Model;
using ZR.Model.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 用户系统消息Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserMsgService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserMsgService : BaseService<SysUserMsg>, ISysUserMsgService
    {
        private readonly IMessageNotifier _messageNotifier;

        public SysUserMsgService(IMessageNotifier messageNotifier)
        {
            _messageNotifier = messageNotifier;
        }

        /// <summary>
        /// 查询用户系统消息列表（当前用户、未删除，租户由 DataPermi 全局过滤）
        /// </summary>
        public PagedInfo<SysUserMsgDto> GetList(SysUserMsgQueryDto parm)
        {
            var predicate = QueryExp(parm);
            var page = GetPages(predicate.ToExpression(), parm, x => x.AddTime, OrderByType.Desc);
            var dtoList = page.Result.Select(m => new SysUserMsgDto
            {
                MsgId = m.MsgId,
                UserId = m.UserId,
                Content = m.Content,
                IsRead = m.IsRead,
                AddTime = m.AddTime,
                TargetId = m.TargetId,
                MsgType = m.MsgType.ToString(),
                IsDelete = m.IsDelete
            }).ToList();
            return new PagedInfo<SysUserMsgDto>
            {
                Result = dtoList,
                PageIndex = page.PageIndex,
                PageSize = page.PageSize,
                TotalNum = page.TotalNum
            };
        }

        /// <summary>
        /// 已读消息（租户由 DataPermi 全局过滤）
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="msgId">消息ID</param>
        /// <param name="msgType">消息类型</param>
        public int ReadMsg(long userId, long msgId, UserMsgType msgType)
        {
            if (msgId == 0)
            {
                return Update(f => f.UserId == userId && f.MsgType == msgType, it => new SysUserMsg() { IsRead = 1 });
            }
            return Update(f => f.UserId == userId && f.MsgId == msgId, it => new SysUserMsg() { IsRead = 1 });
        }

        /// <summary>
        /// 获取详情（租户由 DataPermi 全局过滤）
        /// </summary>
        /// <param name="MsgId">消息ID</param>
        public SysUserMsg GetInfo(long MsgId)
        {
            var response = Queryable()
                .Where(x => x.MsgId == MsgId)
                .First();

            return response;
        }

        /// <summary>
        /// 添加用户系统消息，落库成功后实时推送给在线接收人（异常不影响落库）
        /// </summary>
        public SysUserMsg AddSysUserMsg(SysUserMsg model)
        {
            // 仅当未显式指定租户时，自动填入当前租户ID（INSERT 不经全局过滤，需手动填）
            if (string.IsNullOrEmpty(model.TenantId))
            {
                model.TenantId = App.GetCurrentTenantId();
            }
            Insertable(model).ExecuteReturnSnowflakeId();
            if (model.UserId.HasValue)
            {
                _ = _messageNotifier.NotifyUserAsync(model.UserId.Value, model.Adapt<SysUserMsgDto>());
            }
            return model;
        }
        public SysUserMsg AddSysUserMsg(long userId, string content, UserMsgType msgType)
        {
            return AddSysUserMsg(new SysUserMsg()
            {
                UserId = userId,
                Content = content,
                MsgType = msgType
            });
        }
        /// <summary>
        /// 添加系统消息（显式指定租户ID），供后台任务等无租户上下文场景使用，确保消息正确落入目标租户。
        /// </summary>
        public SysUserMsg AddSysUserMsg(long userId, string content, UserMsgType msgType, string tenantId)
        {
            return AddSysUserMsg(new SysUserMsg()
            {
                UserId = userId,
                Content = content,
                MsgType = msgType,
                TenantId = tenantId
            });
        }

        /// <summary>
        /// 清空用户系统消息
        /// </summary>
        public bool TruncateSysUserMsg()
        {
            var newTableName = $"sys_user_msg_{DateTime.Now:yyyyMMdd}";
            if (Queryable().Any() && !Context.DbMaintenance.IsAnyTable(newTableName))
            {
                Context.DbMaintenance.BackupTable("sys_user_msg", newTableName);
            }

            return Truncate();
        }

        /// <summary>
        /// 导出用户系统消息
        /// </summary>
        public PagedInfo<SysUserMsgDto> ExportList(SysUserMsgQueryDto parm)
        {
            var predicate = QueryExp(parm);
            var page = GetPages(predicate.ToExpression(), parm, x => x.AddTime, OrderByType.Desc);
            var dtoList = page.Result.Select(m => new SysUserMsgDto()).ToList();
            return new PagedInfo<SysUserMsgDto>
            {
                Result = dtoList,
                PageIndex = page.PageIndex,
                PageSize = page.PageSize,
                TotalNum = page.TotalNum
            };
        }

        /// <summary>
        /// 查询表达式（租户及 IsDelete 由 DataPermi 全局过滤，此处仅做参数条件）
        /// </summary>
        private static Expressionable<SysUserMsg> QueryExp(SysUserMsgQueryDto parm)
        {
            var predicate = Expressionable.Create<SysUserMsg>();
            predicate = predicate.AndIF(parm.UserId != null, it => it.UserId == parm.UserId);
            predicate = predicate.AndIF(parm.IsRead != null, it => it.IsRead == parm.IsRead);
            predicate = predicate.AndIF(parm.MsgType != null, it => it.MsgType == parm.MsgType);
            return predicate;
        }

        /// <summary>
        /// 未读消息数（当前用户，租户及 IsDelete 由 DataPermi 全局过滤）
        /// </summary>
        public int UnreadCount(long userId)
        {
            return Queryable().Count(x => x.UserId == userId && x.IsRead == 0);
        }


        /// <summary>
        /// 删除消息（软删除，租户由 DataPermi 全局过滤）
        /// </summary>
        public int DeleteByIds(long[] ids, long userId)
        {
            if (ids == null || ids.Length == 0) return 0;
            return Update(x => ids.Contains(x.MsgId) && x.UserId == userId, it => new SysUserMsg { IsDelete = 1 });
        }

        /// <summary>
        /// 当前用户全部消息标记为已读（租户及 IsDelete 由 DataPermi 全局过滤）
        /// </summary>
        public int ReadAll(long userId)
        {
            return Update(x => x.UserId == userId && x.IsRead == 0, it => new SysUserMsg { IsRead = 1 });
        }

    }
}