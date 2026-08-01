namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 审批评论 / 批注服务
    /// </summary>
    public interface IWfFlowCommentService
    {
        PagedInfo<WfFlowCommentDto> GetList(WfFlowCommentQueryDto parm);
        void Add(WfFlowCommentInput parm, LoginUser user);
    }
}
