using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;
using SqlSugar;
using SqlSugar.IOC;

namespace ZR.Repository
{
    /// <summary>
    /// 代码生成演示仓储接口的实现
    ///
    /// @author zr
    /// @date 2021-09-27
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GendemoRepository : BaseRepository<Gendemo>
    {
        private readonly ISqlSugarClient db;
        public GendemoRepository()
        {
            db = DbScoped.SugarScope.GetConnection(1);
        }

        #region 业务逻辑代码
        public void Test()
        {
            var date = db.GetDate();
        }
        #endregion
    }
}