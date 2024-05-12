using Infrastructure.Attribute;
using Mapster;
using ZR.Model;
using ZR.Model.Content.Dto;
using ZR.Model.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 用户系统消息Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserMsgService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserMsgService : BaseService<SysUserMsg>, ISysUserMsgService
    {
        /// <summary>
        /// 查询用户系统消息列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SysUserMsgDto> GetList(SysUserMsgQueryDto parm)
        {
            var predicate = QueryExp(parm);

            if (parm.MsgType == UserMsgType.COMMENT || parm.MsgType == UserMsgType.PRAISE)
            {
                return Queryable()
                    .Where(predicate.ToExpression())
                    .Includes(x => x.User)
                    .Select((it) => new SysUserMsgDto()
                    {
                        User = it.User.Adapt<UserDto>()
                    }, true)
               .ToPage(parm);
            }
            else
            {
                return Queryable()
                    .Where(predicate.ToExpression())
                    .Select(it => new SysUserMsgDto()
                    {

                    }, true)
               .ToPage(parm);
            }
        }

        /// <summary>
        /// 已读消息
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="msgId"></param>
        /// <param name="msgType"></param>
        /// <returns></returns>
        public int ReadMsg(long userId, long msgId, UserMsgType msgType)
        {
            if (msgId == 0)
            {
                return Update(f => f.UserId == userId && f.MsgType == msgType, it => new SysUserMsg() { IsRead = 1 });
            }
            return Update(f => f.UserId == userId && f.MsgId == msgId, it => new SysUserMsg() { IsRead = 1 });
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="MsgId"></param>
        /// <returns></returns>
        public SysUserMsg GetInfo(long MsgId)
        {
            var response = Queryable()
                .Where(x => x.MsgId == MsgId)
                .First();

            return response;
        }

        /// <summary>
        /// 添加用户系统消息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public SysUserMsg AddSysUserMsg(SysUserMsg model)
        {
            Insertable(model).ExecuteReturnSnowflakeId();
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
        /// 清空用户系统消息
        /// </summary>
        /// <returns></returns>
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
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SysUserMsgDto> ExportList(SysUserMsgQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new SysUserMsgDto()
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
        private static Expressionable<SysUserMsg> QueryExp(SysUserMsgQueryDto parm)
        {
            var predicate = Expressionable.Create<SysUserMsg>();

            predicate = predicate.AndIF(parm.UserId != null, it => it.UserId == parm.UserId);
            predicate = predicate.AndIF(parm.IsRead != null, it => it.IsRead == parm.IsRead);
            predicate = predicate.AndIF(parm.MsgType != null, it => it.MsgType == parm.MsgType);
            //predicate = predicate.AndIF(parm.ClassifyId != null, it => it.ClassifyId == parm.ClassifyId);
            return predicate;
        }
    }
}