using Infrastructure.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI 配置唯一读取入口：文件默认值 + 数据库覆盖。
    /// <para>
    /// 配置源 <c>AiConfig:Source</c>：
    /// <list type="bullet">
    /// <item><c>auto</c>（默认）：Production 读数据库，其它环境只读文件 —— 保证本地调试改 key 不影响线上；</item>
    /// <item><c>db</c>：以数据库为准 —— 库中无配置时回退文件，但<b>读取异常/配置残缺直接失败关闭</b>
        /// （不再静默用文件的旧 Provider/旧 Key 顶上）；</item>
    /// <item><c>file</c>：只读文件（本地连生产库时用这个，彻底隔离）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 全部 AI 代码只认本入口，禁止再直接 <c>AppSettings.Get&lt;AiOptions&gt;()</c>，
    /// 否则会出现“后台改了但某个模块仍读旧值”的双口问题。
    /// </para>
    /// <para>
    /// 唯一例外：<c>AiOptions:PromptDir</c>（见 Infrastructure/AI/AiHelper.cs）仍固定读文件。
    /// 它指向部署目录下的 Prompts 路径，不属于可在后台修改的运行时参数；不要“顺手统一”成本入口，
    /// 否则库中配置不含该项，目录会退回 AppContext.BaseDirectory/Prompts，导致提示词文件找不到。
    /// </para>
    /// </summary>
    public static class AiOptionsResolver
    {
        /// <summary>配置段名（与 ai.json 的节名一致）。</summary>
        public const string SectionName = "AiOptions";

        private static readonly ILogger Logger = AiLog.GetLogger("Infrastructure.AI.AiOptionsResolver");
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly object SyncRoot = new();

        private static string _lastSourceSignature;

        /// <summary>
        /// 取当前生效的 AI 配置：全局参数取数据库（按来源策略）否则回退文件；
        /// 渠道（Providers）优先取 ai_provider 表，库中无渠道时沿用文件里的 Providers。
        /// <para>只关心取值用本方法；排查“到底读了哪一层”请用 <see cref="Resolve"/>。</para>
        /// </summary>
        public static AiOptions Get() => Resolve().Options;

        /// <summary>
        /// 解析当前生效配置，并带回来源、渠道数与告警（诊断接口 / 启动自检使用）。
        /// </summary>
        /// <param name="logSource">
        /// 是否在来源变化时打印“AI 配置生效来源”日志。启动自检传 false，
        /// 由自检自己决定打印内容，避免 AI 未启用时也往 Info 里刷一条来源日志。
        /// </param>
        /// <remarks>
        /// 权威来源约定（与文档「配置来源与优先级」一致）：
        /// <list type="bullet">
        /// <item>全局参数：<c>ai_config</c> 是权威来源，<c>ai.json</c> 只作首次部署种子与库不可用时的兜底；</item>
        /// <item>模型渠道：<c>ai_provider</c> 是唯一权威来源，库中无渠道时才回退文件 <c>Providers</c>；</item>
        /// <item><c>appsettings</c>/环境变量只提供 <c>AiConfig:Source</c> 与 <c>PromptDir</c>，不承载模型密钥。</item>
        /// </list>
        /// </remarks>
        public static AiOptionsResolution Resolve(bool logSource = true)
        {
            var warnings = new List<string>();
            var source = ResolveSource();
            var environment = ResolveEnvironment();
            var databaseEnabled = IsDatabaseSourceEnabled(source);

            var fromDatabase = TryGetFromDatabase(out var sourceDescription, warnings);
            var options = fromDatabase ?? GetFromFile();
            var providerFromDatabase = ApplyDatabaseProviders(options, warnings);

            if (!databaseEnabled && string.Equals(source, "auto", StringComparison.Ordinal))
            {
                warnings.Add($"配置源=auto 且当前环境={environment}（非 Production）：本次只读文件，不读 ai_config / ai_provider；如需与线上一致请显式设置 AiConfig:Source=db。");
            }
            if (databaseEnabled && fromDatabase == null && warnings.Count == 0)
            {
                warnings.Add($"配置源={source} 已启用数据库，但库中无 {SectionName} 段配置，本次整份使用 ai.json 兜底；建议在后台保存一次以固化为权威配置。");
            }
            if (databaseEnabled && !providerFromDatabase)
            {
                warnings.Add("ai_provider 中当前环境没有启用的渠道，本次使用 ai.json 的 Providers；线上请在后台「AI 治理 → 模型渠道」维护。");
            }

            var resolution = new AiOptionsResolution
            {
                Options = options,
                Source = source,
                Environment = environment,
                DatabaseEnabled = databaseEnabled,
                GlobalFromDatabase = fromDatabase != null,
                GlobalSource = fromDatabase != null ? "ai_config" : "ai.json",
                ProviderSource = providerFromDatabase ? "ai_provider" : "ai.json",
                ProviderCount = options?.Providers?.Count ?? 0,
                ActiveProviderName = options?.Providers?
                    .FirstOrDefault(x => string.Equals(x?.Provider, options.Provider, StringComparison.OrdinalIgnoreCase))?.Label,
                Warnings = warnings
            };

            if (logSource)
            {
                LogSourceOnce(sourceDescription, resolution);
            }
            return resolution;
        }

        /// <summary>
        /// 打印一次“本次实际生效的配置来源”，用于启动自检与排查“本地正常、线上异常”。
        /// <para>
        /// AI 未启用（<c>AiOptions.Enable=false</c>）时只写一条 Debug 后跳过：未启用 AI 的站点
        /// 不应被“渠道表中没有启用渠道”“非 Production 只读文件”这类与它无关的告警刷屏。
        /// 需要看配置来源时，后台诊断接口 <c>GET aiGovernance/config/effective</c> 不受此开关影响。
        /// </para>
        /// <para>失败不阻断启动（严格 db 模式下由首次请求把错误回显给管理员）。</para>
        /// </summary>
        public static void LogStartupBanner()
        {
            try
            {
                // 传 false：Info 级别的“AI 配置生效来源”由本方法按启用状态决定是否输出，避免重复/误报
                var resolution = Resolve(logSource: false);
                if (resolution.Options?.Enable != true)
                {
                    Logger.LogDebug(
                        $"AI 功能未启用（AiOptions.Enable=false），跳过配置自检 source={resolution.Source} env={resolution.Environment} global={resolution.GlobalSource} channels={resolution.ProviderSource}");
                    return;
                }

                Logger.LogInformation(
                    $"AI 配置启动自检 source={resolution.Source} env={resolution.Environment} databaseEnabled={resolution.DatabaseEnabled} global={resolution.GlobalSource} channels={resolution.ProviderSource} channelCount={resolution.ProviderCount} provider={resolution.Options?.Provider} model={resolution.Options?.Model}");
                foreach (var warning in resolution.Warnings)
                {
                    Logger.LogWarning($"AI 配置启动自检：{warning}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "AI 配置启动自检失败（不影响站点启动）");
            }
        }

        /// <summary>
        /// 用库中渠道覆盖 Providers：同一 Provider 可能有多行（生产/备用），
        /// 存储层已按优先级排序，这里取第一条即“优先级路由”的最小可用版本。
        /// </summary>
        /// <returns>true 表示 Providers 来自 ai_provider；false 表示沿用文件或未启用数据库。</returns>
        private static bool ApplyDatabaseProviders(AiOptions options, List<string> warnings)
        {
            if (options == null || !IsDatabaseEnabled(out _)) return false;

            IReadOnlyList<AiProviderSnapshot> snapshots;
            try
            {
                snapshots = App.GetService<IAiProviderStore>()?.GetEnabledProviders();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "AI 渠道存储未就绪，沿用文件中的 Providers");
                warnings?.Add("AI 渠道存储未就绪（容器未初始化或 AI 模块未加载），沿用 ai.json 的 Providers。");
                return false;
            }
            if (snapshots == null || snapshots.Count == 0)
            {
                return false;
            }

            options.Providers = snapshots.Select(ToProviderOptions).ToList();

            // 全局 Provider 必须能在渠道表里找到；否则回退到第一条渠道，
            // 避免出现“渠道配了 qwen、全局还写着 openai”时静默走内置默认地址。
            var selected = snapshots.FirstOrDefault(
                x => string.Equals(x.Provider, options.Provider, StringComparison.OrdinalIgnoreCase)) ?? snapshots[0];
            if (!string.Equals(options.Provider, selected.Provider, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(options.Provider))
                {
                    Logger.LogWarning($"AI 全局 Provider={options.Provider} 在渠道表中不存在，已回退使用渠道 {selected.Provider}（{selected.Name}）");
                    warnings?.Add($"全局 Provider={options.Provider} 在 ai_provider 中不存在，已回退使用渠道 {selected.Provider}（{selected.Name}）。");
                }
                options.Provider = selected.Provider;
            }

            // 渠道表是运行时唯一真相：把顶层同名字段对齐到选中渠道。
            // ResolveProvider 是顶层优先，若不清空/对齐，ai_config、ai.json 里残留的顶层
            // ApiKey/BaseUrl/ChatEndpoint/Model 会覆盖渠道值，表现为“后台改了渠道但请求仍用旧 key/旧模型”。
            options.BaseUrl = selected.BaseUrl;
            options.ChatEndpoint = selected.ChatEndpoint;
            options.Model = selected.Model;
            options.ApiKey = selected.ApiKey;

            // 视觉字段同理：顶层 Vision* 会覆盖渠道的 VisionModel/ApiKey，统一交给渠道（及主渠道回退）。
            options.VisionBaseUrl = null;
            options.VisionChatEndpoint = null;
            options.VisionModel = null;
            options.VisionApiKey = null;
            var visionProvider = (options.VisionProvider ?? string.Empty).Trim();
            if (visionProvider.Length > 0
                && !snapshots.Any(x => string.Equals(x.Provider, visionProvider, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogWarning($"AI 视觉 Provider={visionProvider} 在渠道表中不存在，已回退使用主渠道 {selected.Provider}");
                warnings?.Add($"视觉 Provider={visionProvider} 在 ai_provider 中不存在，已回退使用主渠道 {selected.Provider}。");
                options.VisionProvider = null;
            }

            // 候选链：只收与生效 Provider 同名的渠道（不同厂商地址/鉴权不同，不能互相顶替），
            // 首条即上面选中的 selected，其余为备用 Key，供请求失败时切换（见 AiLlmClientRetryWrapper）。
            options.ProviderChain = options.Providers
                .Where(x => string.Equals(x.Provider, options.Provider, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return true;
        }

        /// <summary>渠道投影 → 现有 AiProviderOptions，下游 ResolveProvider 无需改动。</summary>
        private static AiProviderOptions ToProviderOptions(AiProviderSnapshot snapshot) => new()
        {
            Provider = snapshot.Provider,
            Label = snapshot.Name,
            BaseUrl = snapshot.BaseUrl,
            ChatEndpoint = snapshot.ChatEndpoint,
            ApiKey = snapshot.ApiKey,
            Model = snapshot.Model,
            VisionModel = snapshot.VisionModel,
            Models = snapshot.Models,
            TimeoutSeconds = snapshot.TimeoutSeconds
        };

        /// <summary>只读文件配置（诊断与兜底使用）；宿主配置未初始化时返回空配置而不是抛异常。</summary>
        public static AiOptions GetFromFile()
        {
            try
            {
                return AppSettings.Get<AiOptions>(SectionName) ?? new AiOptions();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "读取文件 AI 配置失败（配置源可能未初始化），返回空配置");
                return new AiOptions();
            }
        }

        /// <summary>本次解析是否启用了“数据库覆盖”（供诊断接口展示）。</summary>
        public static bool IsDatabaseEnabled(out string source)
        {
            source = ResolveSource();
            return IsDatabaseSourceEnabled(source);
        }

        private static bool IsDatabaseSourceEnabled(string source)
            => source switch
            {
                "db" => true,
                "file" => false,
                _ => string.Equals(ResolveEnvironment(), "Production", StringComparison.OrdinalIgnoreCase)
            };

        /// <summary>
        /// 从数据库读取全局配置。
        /// <para>
        /// 失败策略按配置源区分：
        /// <list type="bullet">
        /// <item><c>db</c>＝“必须以数据库为准”：读取异常/配置残缺一律<b>失败关闭</b>（抛 CustomException，
        /// 由后台直接回显原因），绝不静默用文件里的旧 Provider/旧 Key 顶上；</item>
        /// <item><c>auto</c>/<c>file</c>＝宽松：异常只告警并回退文件，保证库抖动不至于让 AI 整体不可用。</item>
        /// </list>
        /// </para>
        /// </summary>
        private static AiOptions TryGetFromDatabase(out string sourceDescription, List<string> warnings)
        {
            sourceDescription = null;
            var source = ResolveSource();
            if (!IsDatabaseSourceEnabled(source)) return null;

            var strict = string.Equals(source, "db", StringComparison.Ordinal);
            sourceDescription = strict ? "db" : "auto(db)";

            IAiConfigStore store;
            try
            {
                // 容器未初始化（单元测试、CLI 工具）时 GetService 会抛 NRE：按 Debug 忽略并回退文件
                store = App.GetService<IAiConfigStore>();
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "AI 配置存储未就绪，使用文件配置");
                if (strict) throw ReadFailed(source, "AI 配置存储未就绪（容器未初始化）");
                warnings?.Add("AI 配置存储未就绪（容器未初始化或 AI 模块未加载），本次全局配置使用 ai.json。");
                return null;
            }
            if (store == null)
            {
                if (strict) throw ReadFailed(source, "AI 配置存储未注册（AI 模块可能未加载）");
                warnings?.Add("AI 配置存储未注册（AI 模块可能未加载），本次全局配置使用 ai.json。");
                return null;
            }

            try
            {
                if (!store.TryGetSectionJson(SectionName, out var json, out var error))
                {
                    // 关键区别：这里不是“没有配置”，而是“读不到” —— 严格模式下不能回退文件
                    Logger.LogError($"读取数据库 AI 配置失败 source={source} error={error}");
                    if (strict) throw ReadFailed(source, error ?? "读取数据库失败");
                    warnings?.Add($"读取 ai_config 失败（{error}），本次回退 ai.json；请检查数据库连接。");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    // 合法的空状态（如首次部署尚未在后台保存）：仍按文档回退文件，只是留痕便于排查
                    Logger.LogDebug($"库中没有 {SectionName} 配置，回退文件配置 source={source}");
                    return null;
                }

                var options = JsonSerializer.Deserialize<AiOptions>(json, JsonOptions);
                if (options == null || string.IsNullOrWhiteSpace(options.Provider))
                {
                    Logger.LogWarning($"数据库中的 {SectionName} 缺少 Provider，已回退文件配置 source={source}");
                    if (strict) throw ReadFailed(source, "数据库中的配置缺少 Provider");
                    warnings?.Add($"ai_config 中的 {SectionName} 缺少 Provider，本次回退 ai.json。");
                    return null;
                }
                return options;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 数据库抖动/密文无法解密：严格模式失败关闭，宽松模式回退文件保证 AI 不整体不可用
                Logger.LogError(ex, $"读取数据库 AI 配置失败 source={source}");
                if (strict) throw ReadFailed(source, ex.Message);
                warnings?.Add($"读取 ai_config 异常（{ex.Message}），本次回退 ai.json。");
                return null;
            }
        }

        /// <summary>配置源=db 且读不到配置时的失败关闭异常（CustomException 的消息会原样回显到后台）。</summary>
        private static CustomException ReadFailed(string source, string reason)
            => new(ResultCode.CUSTOM_ERROR,
                $"AI 配置读取失败（AiConfig:Source={source}），已停止使用 AI：{reason}",
                notice: false);

        /// <summary>配置源：auto / file / db（小写，缺省 auto）。</summary>
        private static string ResolveSource()
        {
            try
            {
                var value = AppSettings.App(new[] { "AiConfig", "Source" })?.Trim().ToLowerInvariant();
                return string.IsNullOrWhiteSpace(value) ? "auto" : value;
            }
            catch (Exception)
            {
                return "auto";
            }
        }

        private static string ResolveEnvironment()
            => InternalApp.WebHostEnvironment?.EnvironmentName ?? "Production";

        /// <summary>来源变化时打一条日志（每次请求都会走本方法，不能刷屏）。</summary>
        private static void LogSourceOnce(string sourceDescription, AiOptionsResolution resolution)
        {
            var options = resolution?.Options;
            var signature = $"{sourceDescription ?? "file"}|{resolution?.Environment}|{resolution?.GlobalFromDatabase}|{resolution?.ProviderSource}|{resolution?.ProviderCount}|{options?.Provider}|{options?.Model}";
            lock (SyncRoot)
            {
                if (string.Equals(signature, _lastSourceSignature, StringComparison.Ordinal)) return;
                _lastSourceSignature = signature;
            }

            Logger.LogInformation(
                $"AI 配置生效来源={(resolution?.GlobalFromDatabase == true ? sourceDescription : "file")} global={resolution?.GlobalSource} channels={resolution?.ProviderSource}({resolution?.ProviderCount}) env={resolution?.Environment} provider={options?.Provider} model={options?.Model} enable={options?.Enable}");
        }
    }

    /// <summary>
    /// 一次 <see cref="AiOptionsResolver.Resolve"/> 的结果：生效值 + 来源 + 告警。
    /// 只用于诊断与日志，业务代码继续用 <see cref="AiOptionsResolver.Get"/> 即可。
    /// </summary>
    public sealed class AiOptionsResolution
    {
        /// <summary>最终生效的配置（已合并文件 / ai_config / ai_provider）。</summary>
        public AiOptions Options { get; init; }

        /// <summary>配置源策略：auto / db / file。</summary>
        public string Source { get; init; }

        /// <summary>当前宿主环境名（决定 auto 是否读库）。</summary>
        public string Environment { get; init; }

        /// <summary>本次是否允许读取数据库（auto 仅 Production 为 true）。</summary>
        public bool DatabaseEnabled { get; init; }

        /// <summary>全局参数是否来自 ai_config（false 表示来自 ai.json）。</summary>
        public bool GlobalFromDatabase { get; init; }

        /// <summary>全局参数权威来源：ai_config / ai.json。</summary>
        public string GlobalSource { get; init; }

        /// <summary>渠道权威来源：ai_provider / ai.json。</summary>
        public string ProviderSource { get; init; }

        /// <summary>生效渠道数量。</summary>
        public int ProviderCount { get; init; }

        /// <summary>当前 Provider 对应渠道的展示名（如“主渠道”）。</summary>
        public string ActiveProviderName { get; init; }

        /// <summary>回退、覆盖、环境不一致等需要管理员关注的提示。</summary>
        public List<string> Warnings { get; init; } = new();
    }
}
