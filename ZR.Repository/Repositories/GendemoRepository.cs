using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;

namespace ZR.Repository
{
    /// <summary>
    /// 代码生成演示仓储
    ///
    /// @author zr
    /// @date 2021-12-01
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GendemoRepository : BaseRepository<Gendemo>
    {
        #region 业务逻辑代码
        #endregion
    }
}