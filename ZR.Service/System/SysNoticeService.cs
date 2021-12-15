using Infrastructure;
using Infrastructure.Attribute;
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
        public SysNoticeService(SysNoticeRepository repository) : base(repository)
        {
            _SysNoticerepository = repository;
        }

        #region 业务逻辑代码

        #endregion
    }
}