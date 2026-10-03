using System.Globalization;
using System.Text.RegularExpressions;
using ZR.Mall.Enum;
using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 商品下单表单（买家额外填写的字段）的**唯一定义与校验点**：
    /// 后台保存商品时校验字段定义是否自洽（<see cref="ValidateDefinition"/>），
    /// C 端下单时校验买家填的值（<see cref="ValidateValues"/>）。
    ///
    /// 两处共用本类，各写一套必然出现「后台能存、下单被拒」或「下单放过脏数据」的对不上。
    ///
    /// 设计取舍：
    /// - 类型未收录一律按文本框处理（见 <see cref="ProductFormType.Normalize"/>），不报错。
    /// - 提交值里出现定义外的 key 一律丢弃（不落库）：订单只保留商品定义过的字段。
    /// - 与商品形态解耦：实物/虚拟都能配表单。形态只管地址与运费，不参与本类判断。
    /// - 本类不查库、不碰订单，纯粹是「入参 → 规整结果」；需要订单的场景由调用方自行回填。
    /// </summary>
    public static class ProductFormValidator
    {
        /// <summary>单个商品最多配置多少个字段（再多就该考虑拆商品，而不是一张表单里塞几十项）</summary>
        public const int MaxFieldCount = 20;

        /// <summary>字段键长度上限</summary>
        public const int MaxFieldKeyLength = 50;

        /// <summary>字段显示名长度上限</summary>
        public const int MaxLabelLength = 50;

        /// <summary>候选项文本总长度上限</summary>
        public const int MaxOptionsLength = 500;

        /// <summary>自定义正则长度上限</summary>
        public const int MaxPatternLength = 200;

        /// <summary>未指定 MaxLength 时，单行文本的默认长度上限</summary>
        public const int DefaultMaxLength = 200;

        /// <summary>未指定 MaxLength 时，多行文本的默认长度上限</summary>
        public const int DefaultTextareaMaxLength = 1000;

        /// <summary>单个填写值的硬上限（防止超大文本把 JSON 列撑爆；即便配置了 MaxLength 也不超过它）</summary>
        public const int ValueHardLimit = 2000;

        /// <summary>字段键合法形态：字母开头 + 字母/数字/下划线（要拼进 JSON、下发给前端当 key，故收紧）</summary>
        private static readonly Regex FieldKeyRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

        private static readonly Regex PhoneRegex = new("^1\\d{10}$", RegexOptions.Compiled);

        private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s.]+\\.[^@\\s]+$", RegexOptions.Compiled);

        private static readonly Regex NumberRegex = new("^-?\\d+(\\.\\d+)?$", RegexOptions.Compiled);

        /// <summary>
        /// 取商品的有效字段定义（已归一化：类型落词表、Label 空则回落 Field、按 Sort 排序）。
        /// 返回的是副本，不会改动实体对象。
        /// </summary>
        public static List<ProductFormItem> ResolveFields(Product product)
        {
            var items = product?.FormItems;
            if (items == null || items.Count == 0)
            {
                return new List<ProductFormItem>();
            }

            return items
                .Where(it => it != null && !string.IsNullOrWhiteSpace(it.Field))
                .Select(NormalizeItem)
                .OrderBy(it => it.Sort)
                .ToList();
        }

        /// <summary>该商品是否需要买家额外填写（未配表单返回 false）</summary>
        public static bool HasForm(Product product) => ResolveFields(product).Count > 0;

        /// <summary>
        /// 校验字段定义并**就地归一化**（后台商品新增/修改入口使用）。
        ///
        /// 拦的是"配出来必然不可用"的配置：字段键非法或重复、选择型没候选值、正则写错。
        /// 这类脏配置若放过去，症状要等到买家下单才暴露（提交全部失败），极难定位。
        ///
        /// ⚠️ 必须在商品写入的事务（UseTran）之前调用 —— UseTran 会把异常吞成 result.ErrorMessage，
        /// 之后统一抛「添加/修改失败」，用户看不到具体哪个字段有问题（同 ProductSpecValidator）。
        /// </summary>
        /// <param name="items">字段定义列表，允许为 null/空（不配表单是合法配置）</param>
        /// <exception cref="CustomException">字段定义不自洽</exception>
        public static void ValidateDefinition(List<ProductFormItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            if (items.Count > MaxFieldCount)
            {
                throw new CustomException(ResultCode.PARAM_ERROR,
                    $"下单表单最多配置 {MaxFieldCount} 个字段，当前 {items.Count} 个");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new CustomException(ResultCode.PARAM_ERROR, "存在空的字段配置");
                }

                var field = (item.Field ?? string.Empty).Trim();
                if (field.Length == 0)
                {
                    throw new CustomException(ResultCode.PARAM_ERROR, "字段键不能为空");
                }
                if (field.Length > MaxFieldKeyLength || !FieldKeyRegex.IsMatch(field))
                {
                    throw new CustomException(ResultCode.PARAM_ERROR,
                        $"字段键「{field}」不合法：需以字母开头、只含字母/数字/下划线，且不超过 {MaxFieldKeyLength} 个字符");
                }
                if (!seen.Add(field))
                {
                    throw new CustomException(ResultCode.PARAM_ERROR,
                        $"字段键重复：{field}。同一商品内字段键必须唯一，否则买家填的值会互相覆盖");
                }

                // 就地归一化：类型落词表（未收录→text）、Label 空则回落 Field、候选项去空白去重。
                // 后台下次打开看到的是规范值，不会攒出 input/text 混着存的脏数据。
                item.Field = field;
                item.Type = ProductFormType.Normalize(item.Type);
                item.Label = string.IsNullOrWhiteSpace(item.Label) ? field : item.Label.Trim();
                item.Pattern = string.IsNullOrWhiteSpace(item.Pattern) ? null : item.Pattern.Trim();
                item.Placeholder = string.IsNullOrWhiteSpace(item.Placeholder) ? null : item.Placeholder.Trim();
                // 敏感标记：没设置（null）时按类型给默认，手机号/邮箱自动标上。
                // 运营显式勾选或取消都以他的为准 —— 否则"接收账号"这类必须看清明文的字段
                // 会被类型默认强制成敏感，运营就没法关掉了。
                item.Sensitive ??= ProductFormType.DefaultSensitive(item.Type);
                if (!string.IsNullOrEmpty(item.Options))
                {
                    item.Options = string.Join(",", ParseOptions(item.Options));
                }

                if (item.Label.Length > MaxLabelLength)
                {
                    throw new CustomException(ResultCode.PARAM_ERROR,
                        $"字段「{item.Label}」的名称过长（最多 {MaxLabelLength} 个字符）");
                }

                // 自定义正则：长度兜底 + 必须能编译。写错一次就是「所有买家都提交失败」。
                if (item.Pattern?.Length > MaxPatternLength)
                {
                    throw new CustomException(ResultCode.PARAM_ERROR,
                        $"字段「{item.Label}」的正则过长（最多 {MaxPatternLength} 个字符）");
                }
                if (!string.IsNullOrEmpty(item.Pattern) && !IsValidPattern(item.Pattern))
                {
                    throw new CustomException(ResultCode.PARAM_ERROR,
                        $"字段「{item.Label}」的正则表达式不合法：{item.Pattern}");
                }

                // MaxLength：0 与负数都按"未配置"处理，超过硬上限时运行期会截到硬上限
                if (item.MaxLength is < 0)
                {
                    throw new CustomException(ResultCode.PARAM_ERROR,
                        $"字段「{item.Label}」的长度上限不能为负数");
                }

                if (ProductFormType.NeedOptions(item.Type))
                {
                    var options = ParseOptions(item.Options);
                    if (options.Count == 0)
                    {
                        throw new CustomException(ResultCode.PARAM_ERROR,
                            $"字段「{item.Label}」是 {item.Type} 类型，必须配置候选项（逗号分隔）");
                    }
                    if (item.Options.Length > MaxOptionsLength)
                    {
                        throw new CustomException(ResultCode.PARAM_ERROR,
                            $"字段「{item.Label}」的候选项过多（最多 {MaxOptionsLength} 个字符）");
                    }
                }
            }
        }

        /// <summary>
        /// 校验买家提交的行项表单值，返回**可落库的快照**（C 端下单使用）。
        ///
        /// 规则：
        /// - 商品没配表单 → 直接返回空快照，提交值全部丢弃（不能让任意键值混进订单）；
        /// - 必填留空 → 抛业务异常；非必填留空 → 不进快照（快照只保留真正填了的内容）；
        /// - 自定义正则优先于类型自带规则；
        /// - 选择型的值必须在候选项内，多选逐项校验。
        /// </summary>
        /// <param name="product">下单商品（读取其 FormItems 定义）</param>
        /// <param name="values">买家提交的值，key 为字段键</param>
        /// <exception cref="CustomException">必填缺失、长度超限或格式不合法</exception>
        public static List<OrderFormValue> ValidateValues(Product product, IDictionary<string, string> values)
        {
            var snapshot = new List<OrderFormValue>();
            var fields = ResolveFields(product);
            if (fields.Count == 0)
            {
                return snapshot;
            }

            var submitted = values == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

            foreach (var field in fields)
            {
                submitted.TryGetValue(field.Field, out var raw);
                var value = (raw ?? string.Empty).Trim();
                if (value.Length == 0)
                {
                    if (field.Required)
                    {
                        throw new CustomException(ResultCode.PARAM_ERROR, $"请填写「{field.Label}」");
                    }
                    continue;
                }

                var limit = ResolveMaxLength(field);
                if (value.Length > limit)
                {
                    throw new CustomException(ResultCode.PARAM_ERROR, $"「{field.Label}」长度不能超过 {limit} 个字符");
                }

                CheckValue(field, value);
                snapshot.Add(new OrderFormValue
                {
                    Field = field.Field,
                    Label = field.Label,
                    Type = field.Type,
                    // 快照下单那一刻的敏感标记：商品后来改了配置，历史订单仍按当时的定性强脱敏，
                    // 不会因为运营把字段改成"不敏感"就把旧订单的身份证号明文放出来。
                    Sensitive = field.Sensitive ?? ProductFormType.DefaultSensitive(field.Type),
                    Value = value
                });
            }

            return snapshot;
        }

        /// <summary>
        /// 按类型校验单个值。抛错文案统一带字段名，前端可直接展示给买家。
        /// </summary>
        private static void CheckValue(ProductFormItem field, string value)
        {
            // 自定义正则优先：管理员已明确指定规则时不再叠加类型规则
            // （如"充值账号"既要数字又要限定长度，一个 pattern 就能表达清楚）。
            if (!string.IsNullOrEmpty(field.Pattern))
            {
                Require(IsMatchSafe(value, field.Pattern), field, "格式不正确");
                return;
            }

            switch (field.Type)
            {
                case ProductFormType.Phone:
                    Require(PhoneRegex.IsMatch(value), field, "手机号格式不正确");
                    break;
                case ProductFormType.Email:
                    Require(EmailRegex.IsMatch(value), field, "邮箱格式不正确");
                    break;
                case ProductFormType.Number:
                    Require(NumberRegex.IsMatch(value), field, "只能填数字");
                    break;
                case ProductFormType.Date:
                case ProductFormType.DateTime:
                    Require(DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                        field, "日期格式不正确");
                    break;
                case ProductFormType.Select:
                case ProductFormType.Radio:
                    var single = ParseOptions(field.Options);
                    Require(single.Contains(value, StringComparer.OrdinalIgnoreCase), field,
                        $"只能选择：{string.Join("、", single)}");
                    break;
                case ProductFormType.Checkbox:
                    var multi = ParseOptions(field.Options);
                    var picked = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0);
                    foreach (var part in picked)
                    {
                        Require(multi.Contains(part, StringComparer.OrdinalIgnoreCase), field,
                            $"只能选择：{string.Join("、", multi)}");
                    }
                    break;
                default:
                    // text / textarea / 未收录类型：无附加规则，长度已在上游兜住
                    break;
            }
        }

        private static void Require(bool ok, ProductFormItem field, string message)
        {
            if (!ok)
            {
                throw new CustomException(ResultCode.PARAM_ERROR, $"「{field.Label}」{message}");
            }
        }

        /// <summary>字段值的有效长度上限：配置优先（不超过硬上限），缺省按类型给默认值</summary>
        public static int ResolveMaxLength(ProductFormItem field)
        {
            if (field.MaxLength is > 0)
            {
                return Math.Min(field.MaxLength.Value, ValueHardLimit);
            }
            return DefaultMaxLengthOf(field.Type);
        }

        /// <summary>某类型未配置 MaxLength 时的默认长度上限（随元数据下发，前端据此显示字数提示）</summary>
        public static int DefaultMaxLengthOf(string type)
            => ProductFormType.Normalize(type) == ProductFormType.Textarea ? DefaultTextareaMaxLength : DefaultMaxLength;

        /// <summary>候选项拆分：逗号分隔，去空白、去空项、忽略大小写去重</summary>
        private static List<string> ParseOptions(string options)
        {
            if (string.IsNullOrWhiteSpace(options))
            {
                return new List<string>();
            }
            return options.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ProductFormItem NormalizeItem(ProductFormItem item)
        {
            var field = item.Field.Trim();
            var label = (item.Label ?? string.Empty).Trim();
            var type = ProductFormType.Normalize(item.Type);
            return new ProductFormItem
            {
                Field = field,
                Label = label.Length > 0 ? label : field,
                Type = type,
                Required = item.Required,
                Options = string.IsNullOrWhiteSpace(item.Options) ? null : item.Options.Trim(),
                Pattern = string.IsNullOrWhiteSpace(item.Pattern) ? null : item.Pattern.Trim(),
                MaxLength = item.MaxLength,
                Placeholder = string.IsNullOrWhiteSpace(item.Placeholder) ? null : item.Placeholder.Trim(),
                // 敏感标记只在保存时按类型补齐（见 ValidateDefinition），这里原样带出：
                // 运行期不再推断，判定口径只有"配置里写了什么就是什么"。
                Sensitive = item.Sensitive,
                Sort = item.Sort
            };
        }

        private static bool IsValidPattern(string pattern)
        {
            try
            {
                _ = Regex.IsMatch("test", pattern);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// 历史数据里可能存着写坏的正则（配置校验是后加的），不能让下单因此 500：
        /// 编译失败一律按"不匹配"处理，判定结果是不通过，而不是抛异常。
        /// </summary>
        private static bool IsMatchSafe(string value, string pattern)
        {
            try
            {
                return Regex.IsMatch(value, pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
