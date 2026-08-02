using ZR.ServiceCore.Model;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 流程节点连线（有向边）。**工作流串联的唯一事实来源**。
    ///
    /// 前端应始终为每条边（含直线）生成一条连线（直线 ConditionJson 留空），
    /// 引擎据此决定节点走向：
    /// - 某节点"存在出边"（SourceNodeId 命中）时，引擎按本表连线 + ConditionJson 决定走向；
    /// - 某节点"无出边"时，引擎 fallback 到 NodeOrder 升序取下一节点（仅数据缺失兜底，避免卡死）。
    ///
    /// <see cref="WfFlowNode.NodeOrder"/> 退化为展示排序用途，不再参与流转判断。
    /// 连线与节点同属一份流程定义（按 FlowId 关联），复制/版本/另存新版本时一并连带迁移。
    /// </summary>
    [SugarTable("wf_node_link", "流程节点连线")]
    public class WfNodeLink : SysBase
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 所属流程定义
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long FlowId { get; set; }

        /// <summary>
        /// 源节点Id（连线的起点）
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long SourceNodeId { get; set; }

        /// <summary>
        /// 目标节点Id（连线的终点）
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long TargetNodeId { get; set; }

        /// <summary>
        /// 分支条件（JSON）。为空/null 表示无条件（默认分支）。
        /// 结构对应 <see cref="WfLinkCondition"/>：{ "field", "op", "value" }，op 取值同 <see cref="WfConditionOp"/>。
        /// 引擎（<see cref="Service.WfEngineService.EvalLinkCondition"/>）按单条件解析；
        /// 缺任一字段或解析失败视为条件不满足（保守跳过该连线）。
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string ConditionJson { get; set; }

        /// <summary>
        /// 排序（同一源节点的多条出边，按 Sort 升序评估；越小越优先）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Sort { get; set; } = 0;
    }
}
