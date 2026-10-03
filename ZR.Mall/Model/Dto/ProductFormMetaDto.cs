namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 下单表单设计器元数据：内置字段类型词表 + 各项长度/数量限制。
    ///
    /// 随配置一起下发，是为了让前端不必自己维护一份"类型 → 中文名/默认长度"的映射表
    /// （两边各写一份，迟早对不上）。前端据此渲染类型下拉与字数提示，
    /// 但**判定权仍在服务端**（<see cref="Common.ProductFormValidator"/>）：前端只是体验。
    /// </summary>
    public class ProductFormMetaDto
    {
        /// <summary>可选字段类型（未收录的类型前端不必展示，提交后服务端按文本框处理）</summary>
        public List<ProductFormTypeDto> Types { get; set; }

        /// <summary>
        /// 预设字段清单：后台「表单配置」页直接勾选这些项生成字段定义，
        /// 不做拖拽式表单设计器（字段就那么几类，设计器属于过度设计）。
        /// 运营只改标题、必填、占位提示即可；类型默认取 <see cref="Type"/>，需要时可改。
        /// </summary>
        public List<ProductFormPresetDto> Presets { get; set; }

        /// <summary>单个商品最多可配置的字段数</summary>
        public int MaxFieldCount { get; set; }

        /// <summary>字段键长度上限</summary>
        public int MaxFieldKeyLength { get; set; }

        /// <summary>字段显示名长度上限</summary>
        public int MaxLabelLength { get; set; }

        /// <summary>候选项文本总长度上限</summary>
        public int MaxOptionsLength { get; set; }

        /// <summary>自定义正则长度上限</summary>
        public int MaxPatternLength { get; set; }

        /// <summary>单个填写值的硬上限（配置了 MaxLength 也不会超过它）</summary>
        public int ValueHardLimit { get; set; }
    }

    /// <summary>
    /// 单个字段类型的元数据：值、中文名、是否需要候选项、未配置长度时的默认长度上限。
    /// </summary>
    public class ProductFormTypeDto
    {
        /// <summary>类型值，落库与提交都用它</summary>
        public string Value { get; set; }

        /// <summary>中文显示名</summary>
        public string Label { get; set; }

        /// <summary>是否需要候选项（select/radio/checkbox 为 true，保存时缺候选值会被服务端拒绝）</summary>
        public bool NeedOptions { get; set; }

        /// <summary>
        /// 新建该类型字段时，「收集敏感信息」开关的默认勾选状态（phone/email 为 true）。
        /// 后台设计器据此预置，运营仍可手动改。
        /// </summary>
        public bool DefaultSensitive { get; set; }

        /// <summary>未配置 MaxLength 时该类型的默认长度上限</summary>
        public int DefaultMaxLength { get; set; }
    }

    /// <summary>
    /// 单个预设字段（后台勾选用）。字段定义由它生成：
    /// key 与 type 直接落 <c>Product.FormItems</c>，label / required / placeholder 可改后提交。
    /// </summary>
    public class ProductFormPresetDto
    {
        /// <summary>
        /// 字段键：落库与提交都用它。**一经保存不要再改** ——
        /// 它是下单回传与订单项快照的匹配依据，改了等于换了一个字段。
        /// </summary>
        public string Key { get; set; }

        /// <summary>默认显示名（可改）</summary>
        public string Label { get; set; }

        /// <summary>字段类型，取值见 <see cref="Common.ProductFormValidator"/>；未收录按文本框处理</summary>
        public string Type { get; set; }

        /// <summary>类型中文名（随 <c>types</c> 一起给前端，省得再查表）</summary>
        public string TypeLabel { get; set; }

        /// <summary>是否需要候选项（select/radio/checkbox 为 true）</summary>
        public bool NeedOptions { get; set; }

        /// <summary>勾选后生成字段时的「收集敏感信息」默认状态（接收手机号为 true，接收账号为 false）</summary>
        public bool DefaultSensitive { get; set; }

        /// <summary>默认是否必填（可改）</summary>
        public bool Required { get; set; }

        /// <summary>输入框占位提示（可改）</summary>
        public string Placeholder { get; set; }

        /// <summary>未配置 MaxLength 时该类型的默认长度上限</summary>
        public int DefaultMaxLength { get; set; }
    }
}
