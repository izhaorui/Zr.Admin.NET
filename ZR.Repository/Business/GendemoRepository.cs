using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;

namespace ZR.Repository
{
    /// <summary>
    /// 演示仓储
    ///
    /// @author zz
    /// @date 2022-03-31
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GenDemoRepository : BaseRepository<GenDemo>
    {
        #region 业务逻辑代码
        #endregion
    }
}