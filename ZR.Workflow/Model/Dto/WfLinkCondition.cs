using Newtonsoft.Json;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 流程节点连线分支条件。支持单条件与 And/Or 组合条件（可递归嵌套）。
    ///
    /// 两种形态：
    /// 1) 叶子条件（单个比较）：{ "field": "amount", "op": 3, "value": "10000" }
    ///    - <c>field</c>：表单字段 key（与 <see cref="WfFlowInstance.FormContent"/> 内字段名一致）
    ///    - <c>op</c>：运算符，取值同 <see cref="Enum.WfConditionOp"/>
    ///    - <c>value</c>：比较值（字符串形式，引擎侧按数值/字符串语义比较）
    /// 2) 组合条件：{ "logic": "and"|"or", "conditions": [ 子条件... ] }
    ///    - <c>logic</c>：逻辑组合方式，"and"=全部满足 / "or"=任一满足
    ///    - <c>conditions</c>：子条件数组，每项可为叶子条件或嵌套组合条件（递归）
    ///
    /// 判断规则：<c>Conditions</c> 非空视为组合条件；否则按叶子条件（单比较）求值。
    /// 字段为空字符串（<c>""</c>）表示「无条件/默认分支」，由调用方分流到默认分支路径，不进入条件求值。
    /// 引擎评估（<see cref="Service.WfEngineService.EvalLinkCondition"/>）区分两种失败语义：条件不满足返回 false；
    /// 配置错误（JSON 解析失败 / field / op / value 缺失或 op 无效 / 字段不在表单中）抛出异常并触发事务回滚，
    /// 防止"全部条件因配置错误而不满足 → 流程被误判为正常结束"。
    /// </summary>
    public class WfLinkCondition
    {
        /// <summary>表单字段 key（叶子条件专用）</summary>
        [JsonProperty("field")]
        public string Field { get; set; }

        /// <summary>运算符（对应 <see cref="Enum.WfConditionOp"/> 的数值，叶子条件专用）</summary>
        [JsonProperty("op")]
        public int? Op { get; set; }

        /// <summary>比较值（字符串形式，引擎按数值/字符串语义比较，叶子条件专用）</summary>
        [JsonProperty("value")]
        public string Value { get; set; }

        /// <summary>逻辑组合方式（组合条件专用）："and"=全部满足 / "or"=任一满足。为空字符串视为 and。</summary>
        [JsonProperty("logic")]
        public string Logic { get; set; }

        /// <summary>子条件数组（组合条件专用）：每项可为叶子条件或嵌套组合条件（递归）。非空即按组合求值。</summary>
        [JsonProperty("conditions")]
        public System.Collections.Generic.List<WfLinkCondition> Conditions { get; set; }

        /// <summary>是否组合条件（含子条件数组）。true=按 logic/conditions 求值；false=按 field/op/value 单比较求值。</summary>
        [JsonIgnore]
        public bool IsComposite => Conditions != null && Conditions.Count > 0;
    }
}
