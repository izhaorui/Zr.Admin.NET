using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Service.System.IService
{
    public interface ISysTasksQzService : IBaseService<SysTasksQz>
    {
        //SysTasksQz GetId(object id);
        int AddTasks(SysTasksQz parm);
        int UpdateTasks(SysTasksQz parm);
    }
}
