using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.Common;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.Repository;

namespace ZR.Service.System
{
    /// <summary>
    /// 参数配置Service业务层处理
    ///
    /// @author zhaorui
    /// @date 2021-09-29
    /// </summary>
    [AppService(ServiceType = typeof(ISysConfigService), ServiceLifetime = LifeTime.Transient)]
    public class SysConfigService: BaseService<SysConfig>, ISysConfigService
    {
        private readonly SysConfigRepository _SysConfigrepository;
        public SysConfigService(SysConfigRepository repository)
        {
            _SysConfigrepository = repository;
        }

        #region 业务逻辑代码

        #endregion
    }
}