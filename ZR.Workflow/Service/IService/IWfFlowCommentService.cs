using System.Collections.Generic;
using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 审批评论 / 批注服务
    /// </summary>
    public interface IWfFlowCommentService
    {
        PagedInfo<WfFlowCommentDto> GetList(WfFlowCommentQueryDto parm);
        void Add(WfFlowCommentInput parm, string userName, long userId);
    }
}
