namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 审批记录查询对象
    /// </summary>
    public class WfFlowRecordQueryDto : PagerInfo
    {
        /// <summary>实例Id</summary>
        public long? InstanceId { get; set; }

        /// <summary>申请标题（模糊，关联实例表）</summary>
        public string Title { get; set; }
    }

    /// <summary>
    /// 审批记录对象
    /// </summary>
    public class WfFlowRecordDto : SysBase
    {
        /// <summary>记录Id</summary>
        public long RecordId { get; set; }

        /// <summary>任务Id</summary>
        public long? TaskId { get; set; }

        /// <summary>实例Id</summary>
        public long InstanceId { get; set; }

        /// <summary>节点Id</summary>
        public long? NodeId { get; set; }

        /// <summary>节点名称</summary>
        public string NodeName { get; set; }

        /// <summary>申请标题（冗余，关联实例表）</summary>
        public string Title { get; set; }

        /// <summary>流程名称（冗余，关联实例表）</summary>
        public string FlowName { get; set; }

        /// <summary>申请人（冗余，关联实例表）</summary>
        public string ApplyUser { get; set; }

        /// <summary>实例状态（冗余，关联实例表）</summary>
        public int InstanceStatus { get; set; }

        /// <summary>操作人</summary>
        public string Operator { get; set; }

        /// <summary>动作 0=提交 1=通过 2=驳回 4=撤回</summary>
        public int Action { get; set; } = 0;

        /// <summary>审批意见</summary>
        public string Opinion { get; set; }
    }
}
