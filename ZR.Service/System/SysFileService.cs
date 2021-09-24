using Infrastructure.Attribute;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 文件管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysFileService), ServiceLifetime = LifeTime.Transient)]
    public class SysFileService: BaseService<SysFile>, ISysFileService
    {

    }
}
