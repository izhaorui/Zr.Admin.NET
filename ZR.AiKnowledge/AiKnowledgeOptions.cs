namespace ZR.AiKnowledge
{
    /// <summary>
    /// 知识库插件配置（配置节点 <c>AiKnowledge</c>，见 ai.json）。
    /// <para>
    /// 只含"接哪个外部知识库"的信息，不含切片/向量相关参数——那些由外部知识库自己维护，
    /// 避免本框架重复造轮子（也避免换 embedding 模型时整库重建的运维负担）。
    /// </para>
    /// </summary>
    public class AiKnowledgeOptions
    {
        /// <summary>
        /// 总开关。false（默认）时：<c>search_knowledge</c> 工具不下发模型、前端清单不出现、
        /// 不会发出任何外部请求；无需删工程即可关闭。
        /// </summary>
        public bool Enable { get; set; }

        /// <summary>
        /// 渠道名：generic = 自建 HTTP 检索接口；dify = Dify 数据集检索。
        /// 新增渠道时实现 <see cref="IKnowledgeProvider"/> 并标注 [AppService] 即可，无需改本插件。
        /// </summary>
        public string Provider { get; set; } = GenericProvider;

        /// <summary>
        /// 接口地址。
        /// <para>generic：完整检索 URL，如 https://kb.example.com/api/search。</para>
        /// <para>dify：API 根地址，如 https://api.dify.ai/v1（实际请求 /datasets/{DatasetId}/retrieve）。</para>
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>访问密钥（Bearer）。留空表示不传 Authorization 头。</summary>
        public string ApiKey { get; set; }

        /// <summary>Dify 数据集 ID（Provider=dify 时必填）。</summary>
        public string DatasetId { get; set; }

        /// <summary>知识库标识（generic 渠道原样透传，用于多库路由；可留空）。</summary>
        public string KbId { get; set; }

        /// <summary>回灌给模型时展示的"来源"名称，如"公司制度库"。</summary>
        public string KbName { get; set; } = "知识库";

        /// <summary>默认返回条数（1-20，模型可传 topK 覆盖）。</summary>
        public int TopK { get; set; } = 5;

        /// <summary>相似度下限（0-1，0 表示不启用）。低于该值的片段会被丢弃。</summary>
        public double MinScore { get; set; }

        /// <summary>单次检索超时（秒，1-120）。超时返回友好提示，不重试（检索是只读短请求，重试收益低）。</summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>单个片段回灌给模型的最大字符数，防止超长片段挤占上下文。</summary>
        public int MaxContentChars { get; set; } = 800;

        /// <summary>
        /// 可选权限码。非空时该工具仅对拥有此权限（或管理员）的用户下发，
        /// 与其它 AI 工具同一口径（见 IAiAssistantToolProvider.Permission）。
        /// </summary>
        public string Permission { get; set; }

        /// <summary>generic 渠道名常量</summary>
        public const string GenericProvider = "generic";

        /// <summary>Dify 渠道名常量</summary>
        public const string DifyProvider = "dify";

        /// <summary>把配置值收敛到安全范围，避免异常配置打穿上游。</summary>
        public AiKnowledgeOptions Normalize()
        {
            TopK = Math.Clamp(TopK <= 0 ? 5 : TopK, 1, 20);
            MinScore = MinScore < 0 ? 0 : (MinScore > 1 ? 1 : MinScore);
            TimeoutSeconds = Math.Clamp(TimeoutSeconds <= 0 ? 15 : TimeoutSeconds, 1, 120);
            MaxContentChars = Math.Clamp(MaxContentChars <= 0 ? 800 : MaxContentChars, 200, 4000);
            Provider = string.IsNullOrWhiteSpace(Provider) ? GenericProvider : Provider.Trim().ToLowerInvariant();
            return this;
        }
    }

    /// <summary>
    /// 配置读取入口：每次调用都从 IConfiguration 重新绑定（配置热更新生效，也让测试可直接改配置）。
    /// 宿主未初始化配置时按"未启用"兜底，避免插件把启动流程带崩。
    /// </summary>
    public static class AiKnowledgeSettings
    {
        private const string SectionName = "AiKnowledge";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 配置解析覆盖点：仅供单元测试注入（生产代码保持 null，走 IConfiguration）。
        /// 单元测试串行(同一 Collection)设置与清理，不影响运行时。
        /// </summary>
        public static Func<AiKnowledgeOptions> Resolver { get; set; }

        /// <summary>当前生效配置（已 Normalize，永不为 null）。</summary>
        public static AiKnowledgeOptions Current
        {
            get
            {
                try
                {
                    var resolver = Resolver;
                    if (resolver != null)
                    {
                        return (resolver() ?? new AiKnowledgeOptions()).Normalize();
                    }

                    // 数据库覆盖遵循与 AiOptions 相同的数据源策略（auto 仅在 Production 读库），
                    // 避免本地调试时读到线上的知识库地址与密钥。
                    if (AiOptionsResolver.IsDatabaseEnabled(out var source))
                    {
                        var fromDatabase = TryReadFromDatabase(out var readFailed);
                        if (fromDatabase != null)
                        {
                            return fromDatabase.Normalize();
                        }

                        // 配置源=db 的失败关闭：库“读不到/解不出”与“库里本来就没有该段”必须区别对待，
                        // 前者绝不能回退文件，否则数据库故障期间会继续用文件里的旧地址与旧密钥。
                        if (readFailed && string.Equals(source, "db", StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.Error("读取数据库知识库配置失败（AiConfig:Source=db），已停止使用知识库，不回退文件配置");
                            return new AiKnowledgeOptions();
                        }
                    }

                    return (AppSettings.Bind(SectionName, new AiKnowledgeOptions()) ?? new AiKnowledgeOptions()).Normalize();
                }
                catch (Exception ex)
                {
                    // 配置文件缺失/格式错误时按未启用处理：知识库是可选能力，不能影响主流程
                    Logger.Warn(ex, "读取知识库配置失败，按未启用处理");
                    return new AiKnowledgeOptions();
                }
            }
        }

        /// <summary>
        /// 读取后台维护的库中配置。
        /// <para>
        /// 返回 null 且 <paramref name="readFailed"/>=false：未启用库覆盖，或库中确实没有该段配置（合法空状态）→ 调用方可回退文件；
        /// 返回 null 且 <paramref name="readFailed"/>=true：读取/解析过程失败 → 严格模式（AiConfig:Source=db）必须失败关闭。
        /// </para>
        /// </summary>
        private static AiKnowledgeOptions TryReadFromDatabase(out bool readFailed)
        {
            readFailed = false;
            if (!AiOptionsResolver.IsDatabaseEnabled(out _))
            {
                return null;
            }

            IAiConfigStore store;
            try
            {
                store = App.GetService<IAiConfigStore>();
            }
            catch (Exception ex)
            {
                readFailed = true;
                Logger.Warn(ex, "AI 配置存储未就绪，无法读取库中知识库配置");
                return null;
            }
            if (store == null)
            {
                readFailed = true;
                Logger.Warn("AI 配置存储未注册，无法读取库中知识库配置");
                return null;
            }

            try
            {
                // 必须用 Try 版：GetSectionJson 把“读取失败”和“库里没有配置”都返回 null，
                // 无法在 db 模式下区分“应当失败关闭”和“可以回退文件”。
                if (!store.TryGetSectionJson(SectionName, out var json, out var error))
                {
                    readFailed = true;
                    Logger.Warn($"读取库中知识库配置失败 error={error}");
                    return null;
                }
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                var options = JsonSerializer.Deserialize<AiKnowledgeOptions>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (options == null)
                {
                    readFailed = true;
                    Logger.Warn("库中知识库配置为空内容，无法解析");
                    return null;
                }
                return options;
            }
            catch (Exception ex)
            {
                readFailed = true;
                Logger.Warn(ex, "解析库中知识库配置失败");
                return null;
            }
        }
    }
}
