using Infrastructure.Attribute;
using Infrastructure.Extensions;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;

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
        public List<SysOperLog> GetSysOperLog(SysOperLogDto sysOper, PagerInfo pagerInfo)
        {
            int totalCount = 0;
            var list = Context.Queryable<SysOperLog>()
                .Where(it => it.operTime >= sysOper.BeginTime && it.operTime <= sysOper.EndTime)
                .WhereIF(sysOper.Title.IfNotEmpty(), it => it.title.Contains(sysOper.Title))
                .WhereIF(sysOper.operName.IfNotEmpty(), it => it.operName.Contains(sysOper.operName))
                .WhereIF(sysOper.BusinessType != -1, it =>it.businessType == sysOper.BusinessType)
                .WhereIF(sysOper.Status != -1, it => it.status == sysOper.Status)
                .OrderBy(it => it.OperId, SqlSugar.OrderByType.Desc)
                .ToPageList(pagerInfo.PageNum, pagerInfo.PageSize, ref  totalCount);
            pagerInfo.TotalNum = totalCount;
            return list;
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
