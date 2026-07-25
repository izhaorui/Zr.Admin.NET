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
        And = 1
    }
}
