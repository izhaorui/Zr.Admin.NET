using ZR.Model.System;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 审批任务（每个节点的待办）
    /// </summary>
    [SugarTable("wf_flow_task", "审批任务")]
    public class WfFlowTask : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long TaskId { get; set; }

        /// <summary>
        /// 流程实例Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long InstanceId { get; set; }

        /// <summary>
        /// 节点Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long NodeId { get; set; }

        /// <summary>
        /// 节点名称（冗余）
        /// </summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string NodeName { get; set; }

        /// <summary>
        /// 审批人
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Assignee { get; set; }

        /// <summary>
        /// 任务状态 0=待审 1=已审 2=跳过
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; } = 0;

        /// <summary>
        /// 审批意见
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string Opinion { get; set; }

        /// <summary>
        /// 实际动作 1=通过 2=驳回
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public int? Action { get; set; }

        /// <summary>
        /// 处理时间
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? HandleTime { get; set; }
    }
}
