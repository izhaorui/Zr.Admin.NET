using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("ZR.Workflow")]

namespace Infrastructure.AI
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

        /// <summary>具名占位符形态：{{key}}。用于渲染替换与"渲染后残留"检测。</summary>
        private static readonly Regex PlaceholderPattern = new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled);

        /// <summary>
        /// 进程内记录的“读取失败（缺失或为空）”提示词文件名，供治理自检
        /// （AiGovernanceService.CheckConfiguration）展示，避免"缺提示词"只留在日志里。
        /// 文件名来自各实际调用点，无需另维护一份场景→提示词映射；某文件后续加载成功会自动移除。
        /// </summary>
        private static readonly ConcurrentDictionary<string, byte> MissingFiles = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>读取失败的提示词文件名（升序）。进程内累计；后续加载成功会自动移除。</summary>
        public static IReadOnlyList<string> GetMissingFiles() =>
            MissingFiles.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

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
                if (!string.IsNullOrWhiteSpace(key)) MissingFiles[key] = 0;
            }
            else if (!string.IsNullOrWhiteSpace(key))
            {
                MissingFiles.TryRemove(key, out _);
            }

            lock (_cache)
            {
                _cache[key] = (text, DateTime.Now.Add(CacheSliding));
            }
            return text;
        }

        /// <summary>
        /// 渲染提示词里的具名占位符（<c>{{key}}</c>）：把调用方提供的变量替换进文本。
        /// 渲染后仍残留未替换的占位符会记录告警——调用方漏传变量时模型会看到 <c>{{userId}}</c> 这类
        /// 字面量，属静默降质（不像"文件缺失"那样有治理自检兜底），必须在日志里可见。
        /// </summary>
        /// <param name="text">提示词原文</param>
        /// <param name="variables">变量名（不含花括号）→ 值；值中即使含 <c>$</c> 等字符也按字面替换</param>
        /// <remarks>
        /// 只做具名替换，不做条件/循环等模板语法：提示词里的业务数据（如节点名、指标）应由调用方
        /// 先加工好再传入，模板不承担语义加工。仅对真正走替换的提示词调用本方法——把占位符写进正文
        /// 作"格式说明"的文件（如 lang-translate.md 要求保留 <c>{{var}}</c>）若经过本方法会被误判为未替换。
        /// </remarks>
        public static string Render(string text, IReadOnlyDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 单遍替换：变量值里若恰好含 {{x}} 形态也不会被后续变量二次替换（避免模板注入）
            var rendered = PlaceholderPattern.Replace(text, match =>
            {
                var name = match.Groups[1].Value.Trim();
                return name.Length > 0 && variables != null && variables.TryGetValue(name, out var value)
                    ? value ?? string.Empty
                    : match.Value;
            });

            // 残留只针对"原文里有、但变量表没给"的占位符，不受替换进去的数据内容影响
            var unresolved = FindUnresolvedPlaceholders(text)
                .Where(name => variables == null || !variables.ContainsKey(name))
                .ToList();
            if (unresolved.Count > 0)
            {
                Logger.LogWarning("提示词存在未替换的占位符：{Placeholders}（调用方未提供对应变量，模型将看到占位符字面量）",
                    string.Join("、", unresolved));
            }

            return rendered;
        }

        /// <summary>
        /// 提取文本中仍是占位符形态（<c>{{key}}</c>）的名字：去重、保持出现顺序；无则返回空列表。
        /// 供渲染后自检与单元测试使用。
        /// </summary>
        public static IReadOnlyList<string> FindUnresolvedPlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

            var names = new List<string>();
            foreach (Match match in PlaceholderPattern.Matches(text))
            {
                var name = match.Groups[1].Value.Trim();
                if (name.Length == 0 || names.Contains(name)) continue;
                names.Add(name);
            }
            return names;
        }
    }
}
