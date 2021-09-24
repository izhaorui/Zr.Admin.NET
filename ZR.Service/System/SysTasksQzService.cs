using Infrastructure.Attribute;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    [AppService(ServiceType = typeof(ISysTasksQzService), ServiceLifetime = LifeTime.Transient)]
    public class SysTasksQzService : BaseService<SysTasksQz>, ISysTasksQzService
    {

    }
}
