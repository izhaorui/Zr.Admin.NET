namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 审批任务状态
    /// </summary>
    public enum WfTaskStatus
    {
        /// <summary>待审</summary>
        Pending = 0,
        /// <summary>已审</summary>
        Done = 1,
        /// <summary>跳过</summary>
        Skipped = 2
    }
}
