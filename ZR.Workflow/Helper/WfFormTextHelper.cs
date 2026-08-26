using Newtonsoft.Json.Linq;

namespace ZR.Workflow.Helper
{
    /// <summary>
    /// 表单文本翻译工具：把实例 FormContent（field → value）结合流程定义 FormItems（field → label / type / dictType），
    /// 翻译为"中文label：值"的多行文本，供 AI 上下文使用。
    /// 1) 字段技术名 → 中文label（避免 input_1 暴露）。
    /// 字典绑定字段直接显示存储值（不再做 dictValue → dictLabel 数据库翻译）。
    /// </summary>
    public static class WfFormTextHelper
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 表单字段元数据（解析自 FormItems）。
        /// </summary>
        private sealed class FormItemMeta
        {
            public string Field { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public string DictType { get; set; }
        }

        /// <summary>
        /// 解析 FormItems（JSON 字段数组：field/label/type/dictType）。
        /// </summary>
        private static List<FormItemMeta> ParseFormItems(string formItemsJson)
        {
            var list = new List<FormItemMeta>();
            if (string.IsNullOrWhiteSpace(formItemsJson)) return list;
            try
            {
                var arr = JArray.Parse(formItemsJson);
                foreach (var token in arr)
                {
                    if (token is not JObject item) continue;
                    var field = item["field"]?.ToString();
                    if (string.IsNullOrWhiteSpace(field)) continue;
                    var label = item["label"]?.ToString();
                    var dictType = item["dictType"]?.ToString();
                    list.Add(new FormItemMeta
                    {
                        Field = field,
                        Label = string.IsNullOrWhiteSpace(label) ? field : label,
                        Type = item["type"]?.ToString(),
                        DictType = string.IsNullOrWhiteSpace(dictType) ? null : dictType.Trim()
                    });
                }
            }
            catch (JsonException)
            {
                // 定义异常时不翻译，回退原样
            }
            return list;
        }

        /// <summary>
        /// 把流程定义 FormItems 解析为 field → 中文label 映射。
        /// </summary>
        public static Dictionary<string, string> ParseFieldLabels(string formItemsJson)
        {
            return ParseFormItems(formItemsJson).ToDictionary(m => m.Field, m => m.Label);
        }

        /// <summary>
        /// 把实例 FormContent（field → value）翻译为"中文label：值"的多行文本。
        /// formContent 为空返回 null；解析失败或无可翻译字段时回退原始 formContent 文本。
        /// 字典绑定字段直接显示存储值，不做 dictValue → dictLabel 翻译。
        /// </summary>
        public static string TranslateToText(string formContent, string formItemsJson)
        {
            if (string.IsNullOrWhiteSpace(formContent)) return null;

            var metas = ParseFormItems(formItemsJson);

            Dictionary<string, string> values;
            try
            {
                values = JsonConvert.DeserializeObject<Dictionary<string, string>>(formContent);
            }
            catch (JsonException ex)
            {
                logger.Warn(ex, "TranslateToText: formContent 解析失败，回退原样返回");
                // 按契约回退：formContent 非法 JSON 时原样返回，不阻断调用方
                return formContent;
            }
            if (values == null || values.Count == 0) return formContent;

            var lines = new List<string>(values.Count);
            foreach (var kv in values)
            {
                var meta = metas.FirstOrDefault(m => m.Field == kv.Key);
                var label = meta != null ? meta.Label : kv.Key;
                var val = kv.Value;

                lines.Add($"{label}：{val}");
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 同 <see cref="TranslateToText"/>，但额外识别 image/file 附件字段：
        /// - image 字段：把每个完整 http URL 收集进返回的 ImageUrls（交由视觉模型多模态理解），
        ///   同时在文本中以"【图片附件：(序号) url】"占位提示 AI 结合图片判断；
        /// - file 字段：把每个 URL 交由 WfAttachmentHelper 纯文本抽取，成功则把文本内容拼入文本，
        ///   失败（不支持格式/下载异常）则仅列出文件名，绝不阻断整体。
        /// 返回 (翻译后的表单文本, 图片URL列表)。图片URL为空列表时调用方仍走纯文本 AI 管线。
        /// </summary>
        public static async Task<(string FormText, List<string> ImageUrls)> TranslateToTextWithAttachments(
            string formContent, string formItemsJson)
        {
            if (string.IsNullOrWhiteSpace(formContent))
            {
                return (null, new List<string>());
            }

            var metas = ParseFormItems(formItemsJson);
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(formContent);
            if (values == null || values.Count == 0)
            {
                return (formContent, new List<string>());
            }

            var lines = new List<string>(values.Count);
            var imageUrls = new List<string>();

            foreach (var kv in values)
            {
                var meta = metas.FirstOrDefault(m => m.Field == kv.Key);
                var label = meta != null ? meta.Label : kv.Key;
                var val = kv.Value;

                // 图片附件：收集 URL，文本中以占位提示（白名单外 URL 跳过，避免内网地址泄露给视觉模型）
                if (meta != null && string.Equals(meta.Type, "image", StringComparison.OrdinalIgnoreCase))
                {
                    var urls = SplitUrls(val);
                    foreach (var u in urls)
                    {
                        if (!WfAttachmentHelper.IsUrlAllowed(u)) continue;
                        imageUrls.Add(u);
                        lines.Add($"【图片附件：({imageUrls.Count}) {u}】");
                    }
                    continue;
                }

                // 文件附件：纯文本抽取
                if (meta != null && string.Equals(meta.Type, "file", StringComparison.OrdinalIgnoreCase))
                {
                    var urls = SplitUrls(val);
                    foreach (var u in urls)
                    {
                        var name = FileNameOf(u);
                        var text = await WfAttachmentHelper.ExtractTextAsync(u).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            lines.Add($"【文件附件：{name}】\n{text}");
                        }
                        else
                        {
                            lines.Add($"【文件附件：{name}】（系统无法解析该文件内容，仅附文件名）");
                        }
                    }
                    continue;
                }

                lines.Add($"{label}：{val}");
            }
            logger.Debug("附件解析返回内容: {0}", string.Join("\n", lines));
            return (string.Join("\n", lines), imageUrls);
        }

        /// <summary>
        /// 把逗号分隔（或数组 JSON）的附件值拆成 URL 列表。
        /// </summary>
        private static List<string> SplitUrls(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();
            try
            {
                if (value.TrimStart().StartsWith("["))
                {
                    var arr = JArray.Parse(value);
                    return arr.Select(x => x.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                }
            }
            catch (JsonException ex)
            {
                logger.Warn(ex, "SplitUrls: 解析 JSON 数组失败，按逗号分隔兜底");
                // 不是 JSON 数组，按逗号分隔兜底
            }
            return value.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        /// <summary>
        /// 从 URL 提取文件名（含扩展名）；非法 URL 时回退原样返回。
        /// </summary>
        public static string FileNameOf(string url)
        {
            try
            {
                var path = new Uri(url).AbsolutePath;
                var name = Path.GetFileName(path);
                return string.IsNullOrWhiteSpace(name) ? url : name;
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// 表单字段附件信息（供 WfAttachmentParser 构建 AttachmentParsed 条目）。
        /// </summary>
        public sealed class AttachmentFieldInfo
        {
            public string Field { get; set; }
            public string Label { get; set; }
            public string Type { get; set; }
            public List<string> Urls { get; set; } = new List<string>();
        }

        /// <summary>
        /// 提取 FormContent 中类型为 image/file 的字段及其 URL 列表（按逗号或 JSON 数组分隔）。
        /// 普通字段被忽略，返回仅含附件字段的列表。
        /// </summary>
        public static List<AttachmentFieldInfo> ExtractAttachmentFields(string formContent, string formItemsJson)
        {
            var result = new List<AttachmentFieldInfo>();
            if (string.IsNullOrWhiteSpace(formContent)) return result;

            var metas = ParseFormItems(formItemsJson);
            Dictionary<string, string> values;
            try
            {
                values = JsonConvert.DeserializeObject<Dictionary<string, string>>(formContent)
                         ?? [];
            }
            catch (JsonException ex)
            {
                logger.Warn(ex, "ExtractAttachmentFields: formContent 解析失败，返回空列表");
                return result;
            }

            foreach (var kv in values)
            {
                var meta = metas.FirstOrDefault(m => m.Field == kv.Key);
                if (meta == null) continue;
                var type = meta.Type?.ToLowerInvariant();
                if (type != "image" && type != "file") continue;
                if (string.IsNullOrWhiteSpace(kv.Value)) continue;

                result.Add(new AttachmentFieldInfo
                {
                    Field = kv.Key,
                    Label = meta.Label,
                    Type = type,
                    Urls = SplitUrls(kv.Value)
                });
            }
            return result;
        }
    }
}
