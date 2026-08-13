namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 流程实例服务
    /// </summary>
    public interface IWfFlowInstanceService
    {
        PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, long userId, bool allUser = false);
        WfFlowInstanceDto GetInfo(long instanceId);
        long Start(WfFlowInstanceDto dto, LoginUser user);
        /// <summary>
        /// 驳回后重新提交：申请人修改内容再次发起，回到首节点重新审批。
        /// 参数仅接收变更字段（表单/附件/标题），避免传入整个 DTO 引入意外字段。
        /// </summary>
        /// <param name="instanceId">流程实例 Id</param>
        /// <param name="formContent">表单内容 JSON</param>
        /// <param name="attachment">附件路径（逗号分隔）</param>
        /// <param name="title">申请标题；空则保留实例原标题</param>
        /// <param name="userId">操作人 userId（须为原申请人 ApplyUserId，由 Engine 校验）</param>
        void Resubmit(long instanceId, string formContent, string attachment, string title, long userId);
        /// <summary>
        /// 数据面板统计：待办/已办/我发起/抄送
        /// </summary>
        WfDashboardStatsDto GetDashboardStats(long userId);
        /// <summary>
        /// 流程效率统计：平均审批时长、各节点耗时分布、完成率趋势。
        /// isAdmin=true 时放开为全部用户实例（管理员全局视图）；flowId 可选，按流程定义维度过滤。
        /// </summary>
        WfEfficiencyStatsDto GetEfficiencyStats(long userId, bool isAdmin = false, long? flowId = null);
    }
}
