using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.AI
{
    /// <summary>
    /// 单一渠道的「方言」描述：OpenAI 兼容协议之外，各家在字段名、可选扩展字段、默认地址上的差异。
    /// </summary>
    /// <remarks>
    /// 背景：各家都宣称「OpenAI 兼容」，但只有主干一致，细节各有偏离——qwen(DashScope) 的 usage 用
    /// <c>input_tokens/output_tokens</c>、<c>enable_thinking</c> 是通义私有字段、Ollama 不认
    /// <c>stream_options</c>（带上会挂起或空响应）。这些差异原先以 <c>if (provider == "qwen")</c> /
    /// <c>switch (provider)</c> 散落在 <see cref="AiLlmClient"/> 的六七个方法里，判据是「渠道叫什么名字」
    /// 而非「具备什么能力」，导致新增一家有方言的渠道要改核心大文件多处，且名字不在白名单里的渠道
    /// 即使支持某能力也永远拿不到。<br/>
    /// 现统一为「查能力表」：新增渠道只需在 <see cref="AiProviderProfiles"/> 增改一条，不再动客户端分支。
    /// </remarks>
    public sealed record AiProviderProfile
    {
        /// <summary>渠道标识（规范化小写）。</summary>
        public string Name { get; init; }

        /// <summary>未显式配置 BaseUrl 时的默认地址。</summary>
        public string DefaultBaseUrl { get; init; }

        /// <summary>未显式配置 ChatEndpoint 时的默认端点。</summary>
        public string DefaultEndpoint { get; init; }

        /// <summary>未显式配置 Model 时的默认模型。</summary>
        public string DefaultModel { get; init; }

        /// <summary>是否必须配置 ApiKey（Ollama 等本地服务可留空）。</summary>
        public bool RequiresApiKey { get; init; } = true;

        /// <summary>是否支持 <c>enable_thinking</c> 扩展字段（通义混合思考模型私有）。</summary>
        public bool SupportsThinkingField { get; init; }

        /// <summary>是否支持 <c>stream_options.include_usage</c>（Ollama 等带上会挂起或空响应）。</summary>
        public bool SupportsStreamUsageOptions { get; init; }

        /// <summary>usage 是否用 <c>input_tokens/output_tokens</c> 而非 <c>prompt_tokens/completion_tokens</c>。</summary>
        public bool UsesInputOutputTokens { get; init; }

        /// <summary>
        /// 视觉模型名称判定规则：模型名命中任一片段即视为视觉模型（用于跳过 <c>enable_thinking</c>）。
        /// 默认沿用全局约定（-vl / vl- / vision），渠道可覆盖。
        /// </summary>
        public IReadOnlyList<string> VisionModelPatterns { get; init; } = new[] { "-vl", "vl-", "vision" };

        /// <summary>按名称判定是否为视觉模型（命中任一规则片段）。</summary>
        public bool IsVisionModel(string model)
        {
            var m = (model ?? string.Empty).Trim().ToLowerInvariant();
            if (m.Length == 0) return false;
            return VisionModelPatterns.Any(p => !string.IsNullOrEmpty(p) && m.Contains(p));
        }
    }

    /// <summary>
    /// 渠道能力表：把各家的方言差异集中于此，<see cref="AiLlmClient"/> 只查表、不再做渠道名分支。
    /// 新增渠道：能走标准协议的只改 <c>ai.json</c>；有方言的在此加一条即可，无需改客户端逻辑。
    /// </summary>
    public static class AiProviderProfiles
    {
        /// <summary>协议基准：OpenAI 官方（其余渠道在它之上描述差异）。</summary>
        private static readonly AiProviderProfile OpenAiStandard = new()
        {
            Name = "openai",
            DefaultBaseUrl = "https://api.openai.com",
            DefaultEndpoint = "/v1/chat/completions",
            DefaultModel = "gpt-4o-mini",
            SupportsStreamUsageOptions = true
        };

        /// <summary>
        /// 未登记渠道：地址/端点/模型按 OpenAI 基准，但不主动下发扩展字段（保守，避免未知端挂起）。
        /// 与改造前行为一致（原先未知渠道落 switch 的 <c>_</c> 分支，且不在 stream_options 白名单内）。
        /// </summary>
        private static readonly AiProviderProfile Unknown = OpenAiStandard with
        {
            Name = string.Empty,
            SupportsStreamUsageOptions = false
        };

        private static readonly Dictionary<string, AiProviderProfile> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            // OpenAI 官方：协议基准，无额外方言
            ["openai"] = OpenAiStandard,

            // DeepSeek：端点不带 /v1；usage 用标准字段
            ["deepseek"] = OpenAiStandard with
            {
                Name = "deepseek",
                DefaultBaseUrl = "https://api.deepseek.com",
                DefaultEndpoint = "/chat/completions",
                DefaultModel = "deepseek-chat"
            },

            // 通义千问(DashScope)：BaseUrl 自带 /v1；usage 用 input/output_tokens；独有 enable_thinking
            ["qwen"] = OpenAiStandard with
            {
                Name = "qwen",
                DefaultBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
                DefaultEndpoint = "/chat/completions",
                DefaultModel = "qwen-turbo",
                SupportsThinkingField = true,
                UsesInputOutputTokens = true
            },

            // Ollama：本地服务免 Key；不认 stream_options（带上会挂起或空响应）
            ["ollama"] = OpenAiStandard with
            {
                Name = "ollama",
                DefaultBaseUrl = "http://127.0.0.1:11434/v1",
                DefaultEndpoint = "/chat/completions",
                DefaultModel = "qwen2.5:7b",
                RequiresApiKey = false,
                SupportsStreamUsageOptions = false
            },

            // 智谱 BigModel：其余同 OpenAI 基准，仅需确认认 stream_options（其余字段走 ai.json 显式配置）
            ["bigmodel"] = OpenAiStandard with { Name = "bigmodel" }
        };

        /// <summary>按渠道名查能力表；未登记渠道返回保守的 OpenAI 基准能力。</summary>
        public static AiProviderProfile Resolve(string provider)
        {
            return Known.TryGetValue(Normalize(provider), out var profile) ? profile : Unknown;
        }

        private static string Normalize(string provider) => (provider ?? string.Empty).Trim().ToLowerInvariant();
    }
}
