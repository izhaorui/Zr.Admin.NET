namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程节点输入输出对象
    /// </summary>
    public class WfFlowNodeDto : SysBase
    {
        /// <summary>节点Id（新增时不传）</summary>
        public long NodeId { get; set; }

        /// <summary>流程定义Id</summary>
        public long FlowId { get; set; }

        /// <summary>节点名称</summary>
        [Required(ErrorMessage = "节点名称不能为空")]
        public string NodeName { get; set; }

        /// <summary>节点类型 0=开始 1=审批 2=抄送 3=结束</summary>
        public int NodeType { get; set; } = 1;

        /// <summary>审批人类型 0=指定用户 1=指定角色 2=部门主管</summary>
        public int ApproverType { get; set; } = 0;

        /// <summary>审批人（多个逗号分隔）</summary>
        public string ApproverId { get; set; }

        /// <summary>节点顺序</summary>
        public int NodeOrder { get; set; } = 1;

        /// <summary>签类型 0=或签 1=会签</summary>
        public int SignType { get; set; } = 0;
    }
}
