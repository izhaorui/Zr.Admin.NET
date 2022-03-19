using Infrastructure.Attribute;
using ZR.Model.System;
using ZR.Repository;

namespace ZR.Service.System
{
    /// <summary>
    /// 参数配置Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISysConfigService), ServiceLifetime = LifeTime.Transient)]
    public class SysConfigService : BaseService<SysConfig>, ISysConfigService
    {
        private readonly SysConfigRepository _SysConfigrepository;
        public SysConfigService(SysConfigRepository repository)
        {
            _SysConfigrepository = repository;
        }

        #region 业务逻辑代码

        public SysConfig GetSysConfigByKey(string key)
        {
            return _SysConfigrepository.Queryable().First(f => f.ConfigKey == key);
        }

        #endregion
    }
}