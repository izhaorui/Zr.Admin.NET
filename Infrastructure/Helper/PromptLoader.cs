using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ZR.Workflow")]

namespace Infrastructure.Helper
{
    /// <summary>
    /// 提示词（Prompt）加载器：从磁盘目录读取 .md 提示词文件，带内存缓存。
    /// 设计目标：
    /// 1) 路径可配置（AiOptions.PromptDir），默认 AppContext.BaseDirectory/Prompts；
    /// 2) 文件缺失仅返回 null 并记录告警，<b>不抛异常、不阻断编译与启动</b>；
    /// 3) 调用方在真正使用提示词时再决定是否抛友好异常。
    /// </summary>
    public class PromptLoader
    {
        private static readonly ILogger<PromptLoader> Logger =
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PromptLoader>();

        private static readonly TimeSpan CacheSliding = TimeSpan.FromMinutes(10);
        private readonly string _baseDir;
        private readonly Dictionary<string, (string Text, DateTime Expire)> _cache = new();

        public PromptLoader(string promptDir = null)
        {
            var dir = string.IsNullOrWhiteSpace(promptDir)
                ? Path.Combine(AppContext.BaseDirectory, "Prompts")
                : promptDir;
            _baseDir = Path.GetFullPath(dir);
        }

        /// <summary>
        /// 读取提示词文本。文件不存在或为空时返回 null（不抛异常）。
        /// </summary>
        /// <param name="fileName">文件名，如 flow-generate.md</param>
        public string Load(string fileName)
        {
            var key = fileName ?? string.Empty;
            lock (_cache)
            {
                if (_cache.TryGetValue(key, out var hit) && hit.Expire > DateTime.Now)
                {
                    return hit.Text;
                }
            }

            var path = Path.Combine(_baseDir, fileName);
            string text = null;
            try
            {
                if (File.Exists(path))
                {
                    text = File.ReadAllText(path);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "读取提示词文件失败：{Path}", path);
            }

            if (text == null)
            {
                Logger.LogWarning("提示词文件不存在或为空：{Path}（该 AI 能力调用时将报错）", path);
            }

            lock (_cache)
            {
                _cache[key] = (text, DateTime.Now.Add(CacheSliding));
            }
            return text;
        }
    }
}
