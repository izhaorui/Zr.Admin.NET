using Infrastructure;
using Infrastructure.Attribute;
using SqlSugar;
using System.Collections.Generic;
using ZR.Model.Models;
using ZR.Repository;
using ZR.Repository.System;
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
        private readonly SysNoticeRepository _SysNoticerepository;
        public SysNoticeService(SysNoticeRepository repository)
        {
            _SysNoticerepository = repository;
        }

        #region 业务逻辑代码

        /// <summary>
        /// 查询系统通知
        /// </summary>
        /// <returns></returns>
        public List<SysNotice> GetSysNotices()
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<SysNotice>();

            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.And(m => m.Status == "0");
            return _SysNoticerepository.GetList(predicate.ToExpression());
        }

        #endregion
    }
}