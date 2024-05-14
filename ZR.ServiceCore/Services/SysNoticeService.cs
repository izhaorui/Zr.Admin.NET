using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 通知公告表Service业务层处理
    ///
    /// @author zr
    /// @date 2021-12-15
    /// </summary>
    [AppService(ServiceType = typeof(ISysNoticeService), ServiceLifetime = LifeTime.Transient)]
    public class SysNoticeService : BaseService<SysNotice>, ISysNoticeService
    {
        /// <summary>
        /// 查询系统通知
        /// </summary>
        /// <returns></returns>
        public List<SysNotice> GetSysNotices()
        {
            var predicate = Expressionable.Create<SysNotice>();
            var now = DateTime.Now;
            predicate = predicate.And(m => m.Status == 0);
            predicate = predicate.Or(m => m.BeginTime != null && m.BeginTime <= now && m.EndTime >= now && m.Status == 0);

            return Queryable()
                .Where(predicate.ToExpression())
                .OrderByDescending(f => f.Create_time)
                .ToList();
        }

        public PagedInfo<SysNotice> GetPageList(SysNoticeQueryDto parm)
        {
            var predicate = QueryExp(parm);
            var response = GetPages(predicate.ToExpression(), parm);
            return response;
        }

        /// <summary>
        /// 导出通知公告表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SysNoticeDto> ExportList(SysNoticeQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new SysNoticeDto()
                {
                    NoticeTypeLabel = it.NoticeType.GetConfigValue<SysDictData>("sys_notice_type"),
                    StatusLabel = it.Status.GetConfigValue<SysDictData>("sys_notice_status"),
                }, true)
                .ToPage(parm);

            return response;
        }
        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<SysNotice> QueryExp(SysNoticeQueryDto parm)
        {
            var predicate = Expressionable.Create<SysNotice>();
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.NoticeTitle), m => m.NoticeTitle.Contains(parm.NoticeTitle));
            predicate = predicate.AndIF(parm.NoticeType != null, m => m.NoticeType == parm.NoticeType);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.CreateBy), m => m.Create_by.Contains(parm.CreateBy) || m.Update_by.Contains(parm.CreateBy));
            predicate = predicate.AndIF(parm.Status != null, m => m.Status == parm.Status);
            return predicate;
        }
    }
}