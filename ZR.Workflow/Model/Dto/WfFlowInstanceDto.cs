namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程实例查询对象
    /// </summary>
    public class WfFlowInstanceQueryDto : PagerInfo
    {
        /// <summary>申请标题</summary>
        public string Title { get; set; }
        /// <summary>实例状态</summary>
        public int? Status { get; set; }
        /// <summary>流程定义Id</summary>
        public long? FlowId { get; set; }
    }

    /// <summary>
    /// 流程实例输入输出对象
    /// </summary>
    public class WfFlowInstanceDto : SysBase
    {
        /// <summary>实例Id</summary>
        public long InstanceId { get; set; }

        /// <summary>流程定义Id</summary>
        [Required(ErrorMessage = "请选择流程")]
        public long FlowId { get; set; }

        /// <summary>流程名称</summary>
        public string FlowName { get; set; }

        /// <summary>业务标识</summary>
        public string BusinessKey { get; set; }

        /// <summary>申请标题</summary>
        [Required(ErrorMessage = "申请标题不能为空")]
        public string Title { get; set; }

        /// <summary>申请人</summary>
        public string ApplyUser { get; set; }

        /// <summary>实例状态</summary>
        public int Status { get; set; } = 0;

        /// <summary>当前节点Id</summary>
        public long? CurrentNodeId { get; set; }

        /// <summary>表单内容（JSON）</summary>
        public string FormContent { get; set; }

        /// <summary>附件</summary>
        public string Attachment { get; set; }

        /// <summary>审批任务列表（详情用）</summary>
        public List<WfFlowTaskDto> Tasks { get; set; } = new();

        /// <summary>审批记录列表（详情用）</summary>
        public List<WfFlowRecordDto> Records { get; set; } = new();
    }
}
