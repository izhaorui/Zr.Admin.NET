using System;

namespace Infrastructure.Helper
{
    /// <summary>
    /// 通用 JSON 文本工具：从 LLM / 自然语言输出中提取 JSON 对象。
    /// 用于「从大模型返回内容里扒出可用 JSON」这类场景，剥离 Markdown 代码块、提取首个 JSON 对象。
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 剥离可能的 ```json ``` 代码块包裹，并提取首个 JSON 对象。
        /// 若内容本身已是纯 JSON，原样返回；若提取不到 JSON 对象，返回剥离后的文本（供纯文本场景使用）。
        /// </summary>
        public static string StripMarkdown(string raw)
        {
            var s = (raw ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return s;
            }

            const string fenceStart = "```";
            if (s.StartsWith(fenceStart, StringComparison.Ordinal))
            {
                var firstNewline = s.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    s = s[(firstNewline + 1)..];
                }
                else
                {
                    // 无换行（如 ```{...}```）：手动去掉开头的 ``` 语言标识
                    s = s[fenceStart.Length..];
                }

                var lastFence = s.LastIndexOf(fenceStart, StringComparison.Ordinal);
                if (lastFence > 0)
                {
                    s = s[..lastFence];
                }
                s = s.Trim();
            }

            var extracted = ExtractFirstJsonObject(s);
            return string.IsNullOrWhiteSpace(extracted) ? s : extracted;
        }

        /// <summary>
        /// 从文本中提取首个配对的 JSON 对象（忽略字符串内的 { } 与转义）。找不到返回空串。
        /// 不校验 JSON 合法性，仅按大括号配对截取。
        /// </summary>
        public static string ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var start = -1;
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    if (depth == 0)
                    {
                        continue;
                    }

                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        return text[start..(i + 1)];
                    }
                }
            }

            return string.Empty;
        }
    }
}
