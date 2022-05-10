using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 操作日志
    /// </summary>
    [AppService(ServiceType = typeof(ISysOperLogService), ServiceLifetime = LifeTime.Transient)]
    public class SysOperLogService : BaseService<SysOperLog>, ISysOperLogService
    {
        public SysOperLogRepository sysOperLogRepository;

        public SysOperLogService(SysOperLogRepository sysOperLog)
        {
            sysOperLogRepository = sysOperLog;
        }

        /// <summary>
        /// 新增操作日志操作
        /// </summary>
        /// <param name="operLog">日志对象</param>
        public void InsertOperlog(SysOperLog operLog)
        {
            if (operLog.operParam.Length >= 1000)
            {
                operLog.operParam = operLog.operParam.Substring(0, 1000);
            }
            sysOperLogRepository.AddSysOperLog(operLog);
        }

        /// <summary>
        /// 查询系统操作日志集合
        /// </summary>
        /// <param name="operLog">操作日志对象</param>
        /// <param name="pager"></param>
        /// <returns>操作日志集合</returns>
        public PagedInfo<SysOperLog> SelectOperLogList(SysOperLogDto operLog, PagerInfo pager)
        {
            operLog.BeginTime = DateTimeHelper.GetBeginTime(operLog.BeginTime, -1);
            operLog.EndTime = DateTimeHelper.GetBeginTime(operLog.EndTime, 1);

            bool isDemoMode = AppSettings.GetAppConfig("DemoMode", false);
            if (isDemoMode)
            {
                return new PagedInfo<SysOperLog>();
            }
            var list = sysOperLogRepository.GetSysOperLog(operLog, pager);

            return list;
        }

        /// <summary>
        /// 清空操作日志
        /// </summary>
        public void CleanOperLog()
        {
            sysOperLogRepository.ClearOperLog();
        }

        /// <summary>
        /// 批量删除系统操作日志
        /// </summary>
        /// <param name="operIds">需要删除的操作日志ID</param>
        /// <returns>结果</returns>
        public int DeleteOperLogByIds(long[] operIds)
        {
            return sysOperLogRepository.DeleteOperLogByIds(operIds);
        }

        /// <summary>
        /// 查询操作日志详细
        /// </summary>
        /// <param name="operId">操作ID</param>
        /// <returns>操作日志对象</returns>
        public SysOperLog SelectOperLogById(long operId)
        {
            return sysOperLogRepository.SelectOperLogById(operId);
        }
    }
}
