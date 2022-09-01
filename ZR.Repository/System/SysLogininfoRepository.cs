using Infrastructure.Attribute;
using Infrastructure.Extensions;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using SqlSugar;

namespace ZR.Repository.System
{
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysLogininfoRepository : BaseRepository<SysLogininfor>
    {
        /// <summary>
        /// 查询登录日志
        /// </summary>
        /// <param name="logininfoDto"></param>
        /// <param name="pager"></param>
        /// <returns></returns>
        public PagedInfo<SysLogininfor> GetLoginLog(SysLogininfor logininfoDto, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysLogininfor>();
            exp.And(it => it.LoginTime >= logininfoDto.BeginTime && it.LoginTime <= logininfoDto.EndTime);
            exp.AndIF(logininfoDto.Ipaddr.IfNotEmpty(), f => f.Ipaddr == logininfoDto.Ipaddr);
            exp.AndIF(logininfoDto.UserName.IfNotEmpty(), f => f.UserName.Contains(logininfoDto.UserName));
            exp.AndIF(logininfoDto.Status.IfNotEmpty(), f => f.Status == logininfoDto.Status);
            var query = Context.Queryable<SysLogininfor>()
                .Where(exp.ToExpression())
                .OrderBy(it => it.InfoId, OrderByType.Desc);

            return query.ToPage(pager);
        }

        /// <summary>
        /// 登录日志记录
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor)
        {
            int result = Context.Insertable(sysLogininfor)
                .ExecuteReturnIdentity();
        }

        /// <summary>
        /// 清空登录日志
        /// </summary>
        public void TruncateLogininfo()
        {
            string sql = "truncate table sys_logininfor";

            Context.Ado.ExecuteCommand(sql);
        }

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids)
        {
            return Context.Deleteable<SysLogininfor>().In(ids).ExecuteCommand();
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="user">登录实体</param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto user)
        {
            return Context.Queryable<SysUser>().First(it => it.UserName == user.Username && it.Password == user.Password);
        }

        /// <summary>
        /// 修改登录信息
        /// </summary>
        /// <param name="user"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public void UpdateLoginInfo(LoginBodyDto user, long userId)
        {
            var db = Context;
            db.Updateable(new SysUser() { LoginIP = user.LoginIP, LoginDate = db.GetDate(), UserId = userId })
                .UpdateColumns(it => new { it.LoginIP, it.LoginDate })
                .ExecuteCommand();
        }
    }
}
