using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.Repository;
using ZR.ServiceCore.Monitor.IMonitorService;

namespace ZR.ServiceCore.Monitor
{
    /// <summary>
    /// 用户在线时长Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IUserOnlineLogService), ServiceLifetime = LifeTime.Transient)]
    public class UserOnlineLogService : BaseService<UserOnlineLog>, IUserOnlineLogService
    {
        /// <summary>
        /// 查询用户在线时长列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<UserOnlineLogDto> GetList(UserOnlineLogQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                //.OrderBy("Id desc")
                .Where(predicate.ToExpression())
                .LeftJoin<SysUser>((it, u) => it.UserId == u.UserId)
                .Select((it, u) => new UserOnlineLogDto()
                {
                    NickName = u.NickName
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 添加用户在线时长
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public UserOnlineLog AddUserOnlineLog(UserOnlineLog model)
        {
            if (model.OnlineTime >= 0.5)
            {
                Insertable(model).ExecuteReturnSnowflakeId();
            }
            return model;
        }

        /// <summary>
        /// 导出用户在线时长
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<UserOnlineLogDto> ExportList(UserOnlineLogQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new UserOnlineLogDto()
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
        private static Expressionable<UserOnlineLog> QueryExp(UserOnlineLogQueryDto parm)
        {
            var predicate = Expressionable.Create<UserOnlineLog>();

            predicate = predicate.AndIF(parm.UserId != null, it => it.UserId == parm.UserId);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.UserIP), it => it.UserIP == parm.UserIP);
            predicate = predicate.AndIF(parm.BeginAddTime == null, it => it.AddTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            predicate = predicate.AndIF(parm.BeginAddTime != null, it => it.AddTime >= parm.BeginAddTime);
            return predicate;
        }
    }
}