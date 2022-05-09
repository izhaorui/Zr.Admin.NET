using System;
using Infrastructure.Attribute;
using ZR.Repository.System;
using ZR.Model.Models;

namespace ZR.Repository
{
    /// <summary>
    /// 多语言配置仓储
    ///
    /// @author zr
    /// @date 2022-05-06
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class CommonLangRepository : BaseRepository<CommonLang>
    {
        #region 业务逻辑代码
        #endregion
    }
}