namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 表单字段权限项（钉钉式字段级权限控制，单个字段）。
    /// 由 <see cref="WfFlowNode.FieldPermission"/>（JSON 数组）反序列化而来。
    /// </summary>
    public class WfFieldPermissionItem
    {
        /// <summary>表单字段 key（对应 FormItems 中 field / FormContent 中字段名）</summary>
        public string Field { get; set; }

        /// <summary>
        /// 权限类型：0=可编辑（默认），1=只读，2=隐藏。
        /// 未在 <see cref="WfFlowNode.FieldPermission"/> 中声明的字段默认可编辑。
        /// </summary>
        public int Perm { get; set; }
    }

    /// <summary>
    /// 表单字段权限视图（详情接口按当前查看者视角返回给前端，用于控制字段可见/只读/可编辑）。
    /// 语义：
    /// <list type="bullet">
    /// <item><c>AllEditable=true</c>：全部字段可编辑（发起人填表/节点未配置任何限制），忽略 ReadonlyFields/HiddenFields。</item>
    /// <item><c>ReadonlyFields</c>：只读字段集合。</item>
    /// <item><c>HiddenFields</c>：隐藏字段集合（FormContent 已据此过滤，前端可直接按集合渲染）。</item>
    /// <item>不在 ReadonlyFields/HiddenFields 中的字段=可编辑。</item>
    /// </list>
    /// 历史实例/已结束/申请人查看进行中实例 → ReadonlyFields/HiddenFields 空。
    /// </summary>
    public class WfFieldPermissionView
    {
        /// <summary>是否全部字段可编辑。true 时忽略 ReadonlyFields/HiddenFields。</summary>
        public bool AllEditable { get; set; }

        /// <summary>只读字段集合。</summary>
        public List<string> ReadonlyFields { get; set; } = new();

        /// <summary>隐藏字段集合。FormContent 已过滤掉这些字段。</summary>
        public List<string> HiddenFields { get; set; } = new();
    }
}
