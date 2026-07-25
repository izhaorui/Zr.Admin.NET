namespace ZR.Workflow.Enum
{
    /// <summary>
    /// 审批人类型
    /// </summary>
    public enum WfApproverType
    {
        /// <summary>指定用户（ApproverId 存用户名，逗号分隔）</summary>
        User = 0,

        /// <summary>指定角色（ApproverId 存角色Id，逗号分隔）</summary>
        Role = 1,

        /// <summary>指定部门（ApproverId 存部门Id，逗号分隔，取该部门下所有用户）</summary>
        Dept = 2
    }
}
