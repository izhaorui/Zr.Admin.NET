using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Service.System;
using Infrastructure.Model;

namespace ZR.Service.System.IService
{
    public interface ISysOperLogService
    {
        public void InsertOperlog(SysOperLog operLog);

        /// <summary>
        /// 查询系统操作日志集合
        /// </summary>
        /// <param name="operLog">操作日志对象</param>
        /// <param name="pager"></param>
        /// <returns>操作日志集合</returns>
        public PagedInfo<SysOperLog> SelectOperLogList(SysOperLogDto operLog, PagerInfo pager);

        /// <summary>
        /// 清空操作日志
        /// </summary>
        public void CleanOperLog();

        /// <summary>
        /// 批量删除系统操作日志
        /// </summary>
        /// <param name="operIds">需要删除的操作日志ID</param>
        /// <returns>结果</returns>
        public int DeleteOperLogByIds(long[] operIds);

        /// <summary>
        /// 查询操作日志详细
        /// </summary>
        /// <param name="operId">操作ID</param>
        /// <returns>操作日志对象</returns>
        public SysOperLog SelectOperLogById(long operId);
    }
}
