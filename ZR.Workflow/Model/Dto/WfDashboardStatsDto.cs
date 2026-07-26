namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 工作流数据面板统计
    /// </summary>
    public class WfDashboardStatsDto
    {
        /// <summary>待我审批</summary>
        public int TodoCount { get; set; }
        /// <summary>已办任务</summary>
        public int DoneCount { get; set; }
        /// <summary>我发起-进行中（审批中）</summary>
        public int MyInProgress { get; set; }
        /// <summary>我发起-已完成（通过+驳回）</summary>
        public int MyCompleted { get; set; }
        /// <summary>抄送我的</summary>
        public int CcCount { get; set; }
    }
}
