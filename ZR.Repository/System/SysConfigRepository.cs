using System;
using Infrastructure.Attribute;
using ZR.Model.System;

namespace ZR.Repository
{
    /// <summary>
    /// 参数配置仓储接口的实现
    ///
    /// @author zhaorui
    /// @date 2021-09-29
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysConfigRepository : BaseRepository<SysConfig>
    {
        #region 业务逻辑代码
        #endregion
    }
}