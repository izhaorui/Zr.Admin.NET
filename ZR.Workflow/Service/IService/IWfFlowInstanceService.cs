using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 流程实例服务
    /// </summary>
    public interface IWfFlowInstanceService
    {
        PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, string userName);
        WfFlowInstanceDto GetInfo(long instanceId);
        long Start(WfFlowInstanceDto dto, string userName);
        /// <summary>
        /// 数据面板统计：待办/已办/我发起/抄送
        /// </summary>
        WfDashboardStatsDto GetDashboardStats(string userName);
    }
}
