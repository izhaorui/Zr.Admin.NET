namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 签类型（多人审批时生效）
    /// </summary>
    public enum WfSignType
    {
        /// <summary>或签：一人通过即通过</summary>
        Or = 0,
        /// <summary>会签：需全部通过</summary>
        And = 1,
        /// <summary>依次审批（顺序会签）：多人按 ResolveApprovers 顺序逐个审批，前一人审完才轮到下一人；全部通过才推进，任一驳回则整节点驳回</summary>
        Sequential = 2
    }
}
