namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 条件运算符：节点条件分支使用，与 wf_flow_node.ConditionOp 列对应。
    /// 仅当 字段 + 运算符 + 值 三者齐全时才生效，任一缺失视为无条件（节点必经）。
    /// </summary>
    public enum WfConditionOp
    {
        /// <summary>无（无条件）</summary>
        None = 0,
        /// <summary>小于</summary>
        Lt = 1,
        /// <summary>小于等于</summary>
        Le = 2,
        /// <summary>大于</summary>
        Gt = 3,
        /// <summary>大于等于</summary>
        Ge = 4,
        /// <summary>等于</summary>
        Eq = 5,
        /// <summary>不等于</summary>
        Ne = 6
    }
}
