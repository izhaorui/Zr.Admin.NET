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
        End = 3
    }
}
