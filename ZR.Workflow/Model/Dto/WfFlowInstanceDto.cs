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

        /// <summary>申请人昵称（快照）</summary>
        public string ApplyNickName { get; set; }

        /// <summary>实例状态</summary>
        public int Status { get; set; } = 0;

        /// <summary>当前节点Id（活动节点集合中的首节点，兼容单分支场景）</summary>
        public long? CurrentNodeId { get; set; }

        /// <summary>
        /// 当前活动节点集合（JSON 数组，元素为 nodeId）。
        /// 并行网关节点(7/8)并发时，多分支同时活动的节点 id 都会落入此集合；
        /// 单分支流转时退化为只含 1 个元素的数组，与 <see cref="CurrentNodeId"/> 保持一致。
        /// 详情页/流程图可据此高亮所有"进行中"节点，而非仅首个。
        /// </summary>
        public string CurrentNodeIds { get; set; }

        /// <summary>当前节点名称（按 CurrentNodeId 关联 wf_flow_node 填充）</summary>
        public string CurrentNodeName { get; set; }

        /// <summary>审批人（列表展示用，逗号分隔；进行中为当前待审人，已结束为全部参与审批人）</summary>
        public string Approvers { get; set; }

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
