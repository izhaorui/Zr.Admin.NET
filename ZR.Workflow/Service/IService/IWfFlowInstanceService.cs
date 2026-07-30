namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 流程实例服务
    /// </summary>
    public interface IWfFlowInstanceService
    {
        PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, long userId);
        WfFlowInstanceDto GetInfo(long instanceId);
        long Start(WfFlowInstanceDto dto, LoginUser user);
        /// <summary>
        /// 驳回后重新提交：申请人修改内容再次发起，回到首节点重新审批
        /// </summary>
        void Resubmit(long instanceId, WfFlowInstanceDto dto, string userName);
        /// <summary>
        /// 数据面板统计：待办/已办/我发起/抄送
        /// </summary>
        WfDashboardStatsDto GetDashboardStats(long userId);
    }
}
