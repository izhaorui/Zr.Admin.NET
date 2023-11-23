using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    public interface ISysTasksQzService : IBaseService<SysTasks>
    {
        PagedInfo<SysTasks> SelectTaskList(TasksQueryDto parm);
        //SysTasksQz GetId(object id);
        int AddTasks(SysTasks parm);
        int UpdateTasks(SysTasks parm);
    }
}
