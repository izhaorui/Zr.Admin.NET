namespace ZR.Mall.Enum
{
    /// <summary>
    /// 商品下单表单的字段类型词表（存 JSON 列的字符串值，风格对齐工作流表单 WfFormTemplate.FormItems）。
    ///
    /// 只内置一批常用类型；**未收录的类型一律按文本框处理**（见 <see cref="Normalize"/>）——
    /// 后台新增类型不必先改服务端枚举，老服务端也不会把新类型判成非法配置。
    ///
    /// 各类型的取值规则收敛在 ZR.Mall.Common.ProductFormValidator，本类只管词表。
    /// </summary>
    public static class ProductFormType
    {
        /// <summary>单行文本（默认类型，也是所有未收录类型的兜底）</summary>
        public const string Text = "text";

        /// <summary>多行文本</summary>
        public const string Textarea = "textarea";

        /// <summary>数字</summary>
        public const string Number = "number";

        /// <summary>手机号（校验为 1 开头的 11 位）</summary>
        public const string Phone = "phone";

        /// <summary>邮箱</summary>
        public const string Email = "email";

        /// <summary>日期</summary>
        public const string Date = "date";

        /// <summary>日期时间</summary>
        public const string DateTime = "datetime";

        /// <summary>下拉单选，值必须取自 Options</summary>
        public const string Select = "select";

        /// <summary>单选组，值必须取自 Options</summary>
        public const string Radio = "radio";

        /// <summary>多选组，提交值用逗号分隔，每项都必须是 Options 里的候选值</summary>
        public const string Checkbox = "checkbox";

        /// <summary>
        /// 内置类型白名单：后台表单设计器据此渲染类型下拉。
        /// 不在表内的类型照样能存与提交，只是按 <see cref="Text"/> 处理。
        /// </summary>
        public static readonly string[] BuiltIn =
        {
            Text, Textarea, Number, Phone, Email, Date, DateTime, Select, Radio, Checkbox
        };

        /// <summary>类型中文名：随元数据一起下发，前端不必自己再维护一张映射表</summary>
        private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
        {
            [Text] = "单行文本",
            [Textarea] = "多行文本",
            [Number] = "数字",
            [Phone] = "手机号",
            [Email] = "邮箱",
            [Date] = "日期",
            [DateTime] = "日期时间",
            [Select] = "下拉选择",
            [Radio] = "单选",
            [Checkbox] = "多选",
        };

        /// <summary>取类型显示名；未收录类型按 <see cref="Text"/> 显示</summary>
        public static string Label(string type) => Labels[Normalize(type)];

        /// <summary>
        /// 别名表：工作流表单用 input 表示单行文本，这里做等价归一，
        /// 免得同一套前端渲染器要对接两份词表。
        /// </summary>
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["input"] = Text,
            ["textarea"] = Textarea,
            ["text"] = Text,
        };

        /// <summary>
        /// 归一化字段类型：空串 / 未收录 / 未知一律落到 <see cref="Text"/>（文本框）。
        /// 大小写与首尾空白不敏感。
        /// </summary>
        public static string Normalize(string type)
        {
            var key = (type ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return Text;
            }
            if (Aliases.TryGetValue(key, out var alias))
            {
                return alias;
            }
            return BuiltIn.Contains(key, StringComparer.OrdinalIgnoreCase) ? key.ToLowerInvariant() : Text;
        }

        /// <summary>是否内置（含别名）类型，供后台表单设计器决定是否作为可选项展示</summary>
        public static bool IsBuiltIn(string type)
        {
            var key = (type ?? string.Empty).Trim();
            return Aliases.ContainsKey(key) || BuiltIn.Contains(key, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否需要候选项：只有选择型字段有下拉/单选/多选，缺候选值无法校验，
        /// 保存时会被 <see cref="Common.ProductFormValidator.ValidateDefinition"/> 拦下。
        /// </summary>
        public static bool NeedOptions(string type)
        {
            var normalized = Normalize(type);
            return normalized == Select || normalized == Radio || normalized == Checkbox;
        }
    }
}
