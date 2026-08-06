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
        Condition = 4,
        /// <summary>并行分叉网关：本身不生成任务，到达后同时激活全部出边目标分支（fork），各分支独立推进</summary>
        ParallelFork = 7,
        /// <summary>并行汇聚网关：本身不生成任务，到达后等待所有入边分支均完成才继续（join）</summary>
        ParallelJoin = 8
    }
}
