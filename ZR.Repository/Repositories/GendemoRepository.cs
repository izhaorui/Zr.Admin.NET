using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;

namespace ZR.Repository
{
    /// <summary>
    /// 仓储接口的实现
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class GendemoRepository : BaseRepository
    {
		public GendemoRepository()
        {
        }

        #region 业务逻辑代码

        

        #endregion
    }
}