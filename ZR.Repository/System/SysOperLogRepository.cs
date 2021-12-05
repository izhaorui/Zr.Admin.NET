using Infrastructure.Attribute;
using Infrastructure.Extensions;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using SqlSugar;
using Infrastructure.Model;

namespace ZR.Repository.System
{
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysOperLogRepository : BaseRepository<SysOperLog>
    {
        /// <summary>
        /// 查询操作日志
        /// </summary>
        /// <param name="sysOper"></param>
        /// <param name="pagerInfo">分页数据</param>
        /// <returns></returns>
        public PagedInfo<SysOperLog> GetSysOperLog(SysOperLogDto sysOper, PagerInfo pagerInfo)
        {
            var exp = Expressionable.Create<SysOperLog>();
            exp.And(it => it.operTime >= sysOper.BeginTime && it.operTime <= sysOper.EndTime);
            exp.AndIF(sysOper.Title.IfNotEmpty(), it => it.title.Contains(sysOper.Title));
            exp.AndIF(sysOper.operName.IfNotEmpty(), it => it.operName.Contains(sysOper.operName));
            exp.AndIF(sysOper.BusinessType != -1, it => it.businessType == sysOper.BusinessType);
            exp.AndIF(sysOper.Status != -1, it => it.status == sysOper.Status);

            return GetPages(exp.ToExpression(), pagerInfo, x => x.OperId, OrderByType.Desc);
        }

        /// <summary>
        /// 添加操作日志
        /// </summary>
        /// <param name="sysOperLog"></param>
        /// <returns></returns>
        public void AddSysOperLog(SysOperLog sysOperLog)
        {
            Context.Insertable(sysOperLog).ExecuteCommandAsync();
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void ClearOperLog()
        {
            string sql = "truncate table sys_oper_log";
            Context.Ado.ExecuteCommand(sql);
        }

        /// <summary>
        /// 删除操作日志
        /// </summary>
        /// <param name="operIds"></param>
        /// <returns></returns>
        public int DeleteOperLogByIds(long[] operIds)
        {
            return Context.Deleteable<SysOperLog>().In(operIds).ExecuteCommand();
        }

        /// <summary>
        /// 查询操作日志
        /// </summary>
        /// <param name="operId"></param>
        /// <returns></returns>
        public SysOperLog SelectOperLogById(long operId)
        {
            return Context.Queryable<SysOperLog>().InSingle(operId);
        }
    }
}
