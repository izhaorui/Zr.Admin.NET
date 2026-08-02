using ZR.ServiceCore.Model;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程节点连线输入输出对象
    /// </summary>
    public class WfNodeLinkDto : SysBase
    {
        /// <summary>连线Id（新增时不传）</summary>
        public long Id { get; set; }

        /// <summary>所属流程定义</summary>
        public long FlowId { get; set; }

        /// <summary>源节点Id</summary>
        public long SourceNodeId { get; set; }

        /// <summary>目标节点Id</summary>
        public long TargetNodeId { get; set; }

        /// <summary>分支条件（JSON）。为空表示无条件（默认分支）；非空时结构对应 <see cref="WfLinkCondition"/>：{ field, op, value }</summary>
        public string ConditionJson { get; set; }

        /// <summary>排序（同一起点多条出边按 Sort 升序评估）</summary>
        public int Sort { get; set; } = 0;
    }
}
