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
    /// @author zhaorui
    /// @date 2021-09-24
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GendemoRepository : BaseRepository<Gendemo>
    {
        public GendemoRepository(SqlSugarClient dbContext) : base(dbContext)
        {
        }
        #region 业务逻辑代码
        #endregion
    }
}