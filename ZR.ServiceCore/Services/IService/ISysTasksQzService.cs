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

        /// <summary>
        /// 规范化任务的租户表达式（新建/修改任务时使用）：
        /// 非多租户沿用提交值；非主租户强制落到自身；主租户支持 * / 精确 / 逗号列表，并逐个校验租户存在。
        /// </summary>
        /// <param name="tenantExpression">前端提交的租户表达式</param>
        /// <param name="tenantService">
        /// 租户校验服务，由调用方在请求作用域内传入。
        /// 不使用构造注入：本服务会被 Quartz 通过根容器解析（App.GetRequiredService），
        /// 而 ISysTenantService 的依赖链含 scoped 服务（IEnumerable&lt;ITenantModuleInitializer&gt;、IHubContext 等），
        /// 在根容器下无法解析，会导致所有定时任务在解析阶段直接失败。
        /// </param>
        string NormalizeTenantExpression(string tenantExpression, ISysTenantService tenantService);
    }
}
