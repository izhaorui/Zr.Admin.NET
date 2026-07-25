using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 审批任务服务
    /// </summary>
    public interface IWfFlowTaskService
    {
        PagedInfo<WfFlowTaskDto> GetTodoList(WfFlowTaskQueryDto parm, string userName);
        PagedInfo<WfFlowTaskDto> GetDoneList(WfFlowTaskQueryDto parm, string userName);
    }
}
