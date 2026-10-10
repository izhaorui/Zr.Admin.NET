using Infrastructure.Model;
using System;
using System.Threading.Tasks;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI 能力公共基建，供各模块（系统 AI / 工作流 AI / AI 对话助手）复用：
    /// AI 开关与 ApiKey 校验、提示词加载、单次问答安全调用。
    /// 与 AiLlmClient（底层 HTTP 封装）配合使用，避免各 Service 各自重复实现。
    /// </summary>
    public static class AiHelper
    {
        /// <summary>提示词加载器：从磁盘 Prompts 目录读取 .md（目录可经 AiOptions.PromptDir 配置）。</summary>
        private static readonly PromptLoader PromptLoader =
            new(AppSettings.Get<AiOptions>("AiOptions")?.PromptDir);

        /// <summary>
        /// 统一校验 AI 开关与 ApiKey，未启用/未配置时抛友好异常。
        /// 供各 AI 能力复用，避免重复判断。
        /// </summary>
        public static AiOptions EnsureAiEnabled()
        {
            var options = AppSettings.Get<AiOptions>("AiOptions");
            if (options == null || !options.Enable)
            {
                throw new Exception("AI 功能未启用，请在 appsettings.json 配置 AiOptions.Enable");
            }
            var resolved = AiLlmClient.ResolveProvider(options);
            if (string.IsNullOrWhiteSpace(resolved.ApiKey) && !AiLlmClient.AllowsEmptyApiKey(resolved.Provider))
            {
                throw new Exception("AI 功能未配置 ApiKey，请在 appsettings.json 的 AiOptions 或 Providers 中配置");
            }
            return options;
        }

        /// <summary>
        /// 读取提示词文本。文件不存在或为空时返回 null（不抛异常），
        /// 由调用方决定是否需要友好报错（推荐用 <see cref="GetPromptOrThrow"/>）。
        /// </summary>
        /// <param name="fileName">文件名，如 flow-generate.md</param>
        public static string LoadPrompt(string fileName)
        {
            return PromptLoader.Load(fileName);
        }

        /// <summary>
        /// 读取提示词，缺失时抛出友好异常（含文件名与目录，便于运维补文件）。
        /// </summary>
        public static string GetPromptOrThrow(string fileName, string capability)
        {
            var text = PromptLoader.Load(fileName);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception($"AI 能力「{capability}」所需提示词文件缺失：{fileName}（请检查 AiOptions:PromptDir 指向的 Prompts 目录）");
            }
            return text;
        }

        /// <summary>
        /// 调用大模型并把网络类异常转成用户可读提示。仅用于单次问答型能力。
        /// </summary>
        public static async Task<string> ChatSafeAsync(string system, string user, string scene = null)
        {
            var options = EnsureAiEnabled();
            try
            {
                return await AiLlmClientRetryWrapper.ChatAsyncWithRetry(options, system, user, scene).ConfigureAwait(false);
            }
            catch (AiException ex)
            {
                throw new Exception(ex.UserMessage);
            }
        }

        /// <summary>
        /// 生成会话自动标题：压缩换行为空格并去首尾空白，取前 24 字符，过长追加省略号。
        /// </summary>
        public static string AutoTitle(string message)
        {
            var title = message.Replace("\r", " ").Replace("\n", " ").Trim();
            return title.Length <= 24 ? title : title[..24] + "…";
        }

        /// <summary>
        /// 文本截断：null/空返回空串，超过 maxLen 截断并追加省略号。用于日志与消息回灌等展示场景，
        /// 让截断痕迹可见；结构化的入库/回传字段请用 <see cref="Truncate"/>。
        /// </summary>
        public static string ClipText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text ?? "";
            return text[..maxLen] + "…";
        }

        /// <summary>
        /// 精确截断：null/空原样返回（保持 null），超过 maxLen 直接截断、不追加省略号。
        /// 用于入库或发给模型的结构化字段——追加省略号会让数据失真（如 langKey/columnName 被回传时对不上号）。
        /// </summary>
        public static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
            return text[..maxLen];
        }

        /// <summary>
        /// 宽松解析日期字符串（供 AI 工具参数解析，如 startDate=2026-09-06），解析失败返回 null。
        /// </summary>
        public static DateTime? TryParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var d) ? d : null;
        }
    }
}
