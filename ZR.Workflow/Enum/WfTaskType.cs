namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 任务类型：区分审批待办与抄送知会
    /// （wf_flow_task.Status=Skipped 同时承载"被跳过的审批"与"抄送"，仅靠状态无法区分，故引入类型字段）
    /// </summary>
    public enum WfTaskType
    {
        /// <summary>审批</summary>
        Audit = 0,
        /// <summary>抄送</summary>
        Cc = 1
    }
}
