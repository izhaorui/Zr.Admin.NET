using System.Collections.Generic;
using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 审批任务服务
    /// </summary>
    public interface IWfFlowTaskService
    {
        PagedInfo<WfFlowTaskDto> GetTodoList(WfFlowTaskQueryDto parm, long userId);
        PagedInfo<WfFlowTaskDto> GetDoneList(WfFlowTaskQueryDto parm, long userId);
        void Read(List<long> ids, long userId);
        /// <summary>当前用户待办未读数量（Status=Pending 且 IsRead=false）</summary>
        int GetUnreadCount(long userId);
    }
}
