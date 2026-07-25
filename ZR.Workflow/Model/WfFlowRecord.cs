using ZR.Model.System;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 审批记录（流水轨迹）
    /// </summary>
    [SugarTable("wf_flow_record", "审批记录")]
    public class WfFlowRecord : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long RecordId { get; set; }

        /// <summary>
        /// 关联任务Id
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? TaskId { get; set; }

        /// <summary>
        /// 流程实例Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long InstanceId { get; set; }

        /// <summary>
        /// 节点Id
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public long? NodeId { get; set; }

        /// <summary>
        /// 操作人
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Operator { get; set; }

        /// <summary>
        /// 动作 0=提交 1=通过 2=驳回 4=撤回
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Action { get; set; } = 0;

        /// <summary>
        /// 审批意见
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string Opinion { get; set; }
    }
}
