using Newtonsoft.Json;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程节点连线分支条件。序列化形式为 JSON 对象，结构与本类 1:1。
    ///
    /// 示例：{ "field": "amount", "op": 3, "value": "10000" }
    /// - <c>field</c>：表单字段 key（与 <see cref="WfFlowInstance.FormContent"/> 内字段名一致）
    /// - <c>op</c>：运算符，取值同 <see cref="Enum.WfConditionOp"/>
    /// - <c>value</c>：比较值（始终以字符串形式保存，引擎侧按可解析为数值则数值比较、否则按字符串 OrdinalIgnoreCase 比较）
    ///
    /// 字段为空字符串（<c>""</c>）表示「无条件/默认分支」，由调用方分流到默认分支路径，不进入 EvalLinkCondition。
    /// 引擎评估（<see cref="Service.WfEngineService.EvalLinkCondition"/>）区分两种失败语义：条件不满足返回 false；
    /// 配置错误（JSON 解析失败 / field / op / value 缺失或 op 无效 / 字段不在表单中）抛出异常并触发事务回滚，
    /// 防止"全部条件因配置错误而不满足 → 流程被误判为正常结束"。
    /// </summary>
    public class WfLinkCondition
    {
        /// <summary>表单字段 key</summary>
        [JsonProperty("field")]
        public string Field { get; set; }

        /// <summary>运算符（对应 <see cref="Enum.WfConditionOp"/> 的数值）</summary>
        [JsonProperty("op")]
        public int? Op { get; set; }

        /// <summary>比较值（字符串形式，引擎按数值/字符串语义比较）</summary>
        [JsonProperty("value")]
        public string Value { get; set; }
    }
}
