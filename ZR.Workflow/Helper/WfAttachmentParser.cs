namespace ZR.Workflow.Helper
{
    /// <summary>
    /// 提交时解析附件的静态工具：把表单中的 image/file 字段解析为可落库的条目列表。
    /// image 仅占位（不抽文本），file 抽取纯文本；解析失败/不支持的格式跳过文本，仅保留文件名占位。
    /// </summary>
    public static class WfAttachmentParser
    {
        /// <summary>
        /// 单条附件解析结果（对应落库 JSON 数组的一项）。
        /// </summary>
        public sealed class AttachmentParseItem
        {
            public string Url { get; set; }
            public string FileName { get; set; }
            public string FileType { get; set; } // "image" | "file"
            public string Text { get; set; }      // 纯文本抽取结果，image/不支持时为 null
            public string Metadata { get; set; }  // 扩展元数据 JSON，暂留空
            public string Fields { get; set; }    // 结构化字段（阶段B），当前留空 {}
        }

        /// <summary>
        /// 解析表单中的附件字段，返回条目列表（顺序：image 在前，file 在后）。
        /// formContent/formItemsJson 为空时返回空列表。
        /// </summary>
        public static async Task<List<AttachmentParseItem>> ParseAsync(string formContent, string formItemsJson)
        {
            var result = new List<AttachmentParseItem>();
            if (string.IsNullOrWhiteSpace(formContent)) return result;

            var fields = WfFormTextHelper.ExtractAttachmentFields(formContent, formItemsJson);
            foreach (var f in fields)
            {
                foreach (var url in f.Urls)
                {
                    var item = new AttachmentParseItem
                    {
                        Url = url,
                        FileName = WfFormTextHelper.FileNameOf(url),
                        FileType = f.Type,
                        Metadata = string.Empty,
                        Fields = "{}"
                    };

                    if (string.Equals(f.Type, "image", StringComparison.OrdinalIgnoreCase))
                    {
                        // 图片仅占位，文本交给视觉模型
                        item.Text = null;
                    }
                    else
                    {
                        var text = await WfAttachmentHelper.ExtractTextAsync(url).ConfigureAwait(false);
                        item.Text = string.IsNullOrWhiteSpace(text) ? null : text;
                    }

                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// 序列化条目列表为落库 JSON 字符串。
        /// </summary>
        public static string Serialize(List<AttachmentParseItem> items)
        {
            return JsonConvert.SerializeObject(items ?? new List<AttachmentParseItem>());
        }

        /// <summary>
        /// 反序列化落库 JSON 字符串；空/非法返回空列表。
        /// </summary>
        public static List<AttachmentParseItem> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<AttachmentParseItem>();
            try
            {
                return JsonConvert.DeserializeObject<List<AttachmentParseItem>>(json)
                       ?? new List<AttachmentParseItem>();
            }
            catch (JsonException)
            {
                return new List<AttachmentParseItem>();
            }
        }

        /// <summary>
        /// 判断条目是否为图片（不抽文本）。
        /// </summary>
        public static bool IsImage(AttachmentParseItem item)
        {
            return item != null
                   && string.Equals(item.FileType, "image", StringComparison.OrdinalIgnoreCase);
        }
    }
}
