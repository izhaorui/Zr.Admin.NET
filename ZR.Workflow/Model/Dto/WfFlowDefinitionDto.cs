namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程定义查询对象
    /// </summary>
    public class WfFlowDefinitionQueryDto : PagerInfo
    {
        /// <summary>流程名称</summary>
        public string FlowName { get; set; }
        /// <summary>状态</summary>
        public int? Status { get; set; }
    }

    /// <summary>
    /// 流程定义输入输出对象
    /// </summary>
    public class WfFlowDefinitionDto : SysBase
    {
        /// <summary>流程定义Id</summary>
        public long FlowId { get; set; }

        /// <summary>流程编码</summary>
        [Required(ErrorMessage = "流程编码不能为空")]
        public string FlowCode { get; set; }

        /// <summary>流程名称</summary>
        [Required(ErrorMessage = "流程名称不能为空")]
        public string FlowName { get; set; }

        /// <summary>表单类型</summary>
        public int FormType { get; set; } = 0;

        /// <summary>状态</summary>
        public int Status { get; set; } = 1;

        /// <summary>表单字段定义（JSON）</summary>
        public string FormItems { get; set; }

        /// <summary>节点配置</summary>
        public List<WfFlowNodeDto> Nodes { get; set; } = new();
    }
}
