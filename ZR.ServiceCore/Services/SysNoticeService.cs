using Infrastructure.Attribute;
using SqlSugar;
using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Service.System.IService;

namespace ZR.Service.System
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
        #region 业务逻辑代码

        /// <summary>
        /// 查询系统通知
        /// </summary>
        /// <returns></returns>
        public List<SysNotice> GetSysNotices()
        {
            var predicate = Expressionable.Create<SysNotice>();

            predicate = predicate.And(m => m.Status == 0);
            return Queryable()
                .Where(predicate.ToExpression())
                .OrderByDescending(f => f.Create_time)
                .ToList();
        }

        public PagedInfo<SysNotice> GetPageList(SysNoticeQueryDto parm)
        {
            var predicate = Expressionable.Create<SysNotice>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.NoticeTitle), m => m.NoticeTitle.Contains(parm.NoticeTitle));
            predicate = predicate.AndIF(parm.NoticeType != null, m => m.NoticeType == parm.NoticeType);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.CreateBy), m => m.Create_by.Contains(parm.CreateBy) || m.Update_by.Contains(parm.CreateBy));
            predicate = predicate.AndIF(parm.Status != null, m => m.Status == parm.Status);
            var response = GetPages(predicate.ToExpression(), parm);
            return response;
        }

        #endregion
    }
}