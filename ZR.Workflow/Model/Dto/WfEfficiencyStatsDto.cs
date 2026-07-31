namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程效率统计
    /// </summary>
    public class WfEfficiencyStatsDto
    {
        /// <summary>已完成（通过）实例数</summary>
        public int FinishedCount { get; set; }
        /// <summary>平均审批时长（小时，保留 2 位小数）</summary>
        public decimal AvgDurationHours { get; set; }
        /// <summary>最短审批时长（小时）</summary>
        public decimal MinDurationHours { get; set; }
        /// <summary>最长审批时长（小时）</summary>
        public decimal MaxDurationHours { get; set; }
        /// <summary>各节点平均耗时分布</summary>
        public List<WfNodeDurationDto> NodeDurations { get; set; } = new();
        /// <summary>流程完成率趋势（按月）</summary>
        public List<WfCompletionTrendDto> CompletionTrend { get; set; } = new();
    }

    /// <summary>
    /// 单个节点的平均耗时
    /// </summary>
    public class WfNodeDurationDto
    {
        /// <summary>节点名称</summary>
        public string NodeName { get; set; }
        /// <summary>平均耗时（小时，保留 2 位小数）</summary>
        public decimal AvgHours { get; set; }
        /// <summary>样本数（已处理任务数）</summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// 按月统计的流程完成率趋势
    /// </summary>
    public class WfCompletionTrendDto
    {
        /// <summary>月份，格式 yyyy-MM</summary>
        public string Month { get; set; }
        /// <summary>当月结束（通过+驳回）的实例总数</summary>
        public int TotalFinished { get; set; }
        /// <summary>当月通过的实例数</summary>
        public int Approved { get; set; }
        /// <summary>当月驳回的实例数</summary>
        public int Rejected { get; set; }
        /// <summary>完成率（%），通过数 / 结束总数，保留 1 位小数</summary>
        public decimal Rate { get; set; }
    }
}
