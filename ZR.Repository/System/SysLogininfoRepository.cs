using Infrastructure.Attribute;
using Infrastructure.Extensions;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;

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
        public List<SysLogininfor> GetLoginLog(SysLogininfor logininfoDto, PagerInfo pager)
        {
            int totalCount = 0;
            var list = Context.Queryable<SysLogininfor>()
                .Where(it => it.loginTime >= logininfoDto.BeginTime && it.loginTime <= logininfoDto.EndTime)
                .WhereIF(logininfoDto.ipaddr.IfNotEmpty(), f => f.ipaddr == logininfoDto.ipaddr)
                .WhereIF(logininfoDto.userName.IfNotEmpty(), f => f.userName.Contains(logininfoDto.userName))
                .WhereIF(logininfoDto.status.IfNotEmpty(), f => f.status == logininfoDto.status)
                .OrderBy(it => it.infoId, SqlSugar.OrderByType.Desc)
                .IgnoreColumns(it => new { it.Create_by, it.Create_time, it.Update_by, it.Update_time, it.Remark })
                .ToPageList(pager.PageNum, pager.PageSize, ref totalCount);
            pager.TotalNum = totalCount;
            return list;
        }

        /// <summary>
        /// 登录日志记录
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor)
        {
            int result = Context.Insertable(sysLogininfor)
                .IgnoreColumns(it => new { it.Create_by, it.Create_time, it.Remark })
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
