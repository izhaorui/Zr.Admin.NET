using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;
using SqlSugar;

namespace ZR.Repository
{
    /// <summary>
    /// 代码生成演示仓储接口的实现
    ///
    /// @author zr
    /// @date 2021-11-24
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GendemoRepository : BaseRepository<Gendemo>
    {
        public GendemoRepository()
        { 
        }

        #region 业务逻辑代码
        #endregion
    }
}