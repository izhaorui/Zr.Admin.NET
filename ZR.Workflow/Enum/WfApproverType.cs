namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 审批人类型
    /// </summary>
    public enum WfApproverType
    {
        /// <summary>指定用户（ApproverId 存 userId，逗号分隔；兼容旧存量 userName）</summary>
        User = 0,

        /// <summary>指定角色（ApproverId 存角色Id，逗号分隔）</summary>
        Role = 1,

        /// <summary>指定部门（ApproverId 存部门Id，逗号分隔，取该部门下所有用户）</summary>
        Dept = 2,

        /// <summary>表单字段（ApproverId 存表单字段 key，字段值为 userId 逗号分隔，运行时从表单动态解析审批人）</summary>
        Field = 3
    }
}
