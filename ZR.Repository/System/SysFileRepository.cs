using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 文件存储仓储
    ///
    /// @author zz
    /// @date 2021-12-15
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysFileRepository : BaseRepository<SysFile>
    {
        #region 业务逻辑代码
        #endregion
    }
}