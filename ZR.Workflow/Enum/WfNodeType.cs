namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 流程节点类型
    /// </summary>
    public enum WfNodeType
    {
        /// <summary>开始节点</summary>
        Start = 0,
        /// <summary>审批节点</summary>
        Audit = 1,
        /// <summary>抄送节点</summary>
        Cc = 2,
        /// <summary>结束节点</summary>
        End = 3,
        /// <summary>条件网关（菱形）：本身不生成任务，到达后按出边 ConditionJson 选一路继续</summary>
        Condition = 4
    }
}
