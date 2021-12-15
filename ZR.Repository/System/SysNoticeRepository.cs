using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;

namespace ZR.Repository.System
{
    /// <summary>
    /// 通知公告表仓储
    ///
    /// @author zr
    /// @date 2021-12-15
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysNoticeRepository : BaseRepository<SysNotice>
    {
        #region 业务逻辑代码
        #endregion
    }
}