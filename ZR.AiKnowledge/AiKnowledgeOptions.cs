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
        /// 是否记录检索审计（默认开启）。
        /// <para>
        /// 开启后每次 <c>search_knowledge</c> 调用在 <c>ai_knowledge_query_log</c> 落一行：
        /// 问题（截断 500 字）、渠道、命中数、耗时、错误分类。用于回答"知识库到底有没有用、
        /// 没答上来是因为没命中还是因为上游故障"。只记检索行为，不复制片段正文。
        /// </para>
        /// <para>
        /// 关闭后不写任何审计行，检索链路本身不受影响；表不存在或写入失败也只记日志。
        /// </para>
        /// </summary>
        public bool AuditEnabled { get; set; } = true;

        /// <summary>单个片段回灌给模型的最大字符数，防止超长片段挤占上下文。</summary>
        public int MaxContentChars { get; set; } = 800;

        /// <summary>
        /// 可选权限码。非空时该工具仅对拥有此权限（或管理员）的用户下发，
        /// 与其它 AI 工具同一口径（见 IAiAssistantToolProvider.Permission）。
        /// </summary>
        public string Permission { get; set; }

        /// <summary>
        /// 知识库列表（唯一配置入口）：每项自带连接信息，不存在顶层单库回落。
        /// <para>数组顺序即默认优先级；<see cref="AiKnowledgeBaseOptions.IsDefault"/>=true 的作为默认库，都未标记时取第一个。</para>
        /// </summary>
        public List<AiKnowledgeBaseOptions> KnowledgeBases { get; set; } = new();

        /// <summary>
        /// 解析实际生效的库列表：过滤停用项、补 Id、去重、收敛数值区间、标记默认库。
        /// <para>每次调用重新计算（配置可热更新）；数量有上限，纯内存运算，无需缓存。</para>
        /// </summary>
        public List<AiKnowledgeBaseOptions> BuildBases()
        {
            var configured = (KnowledgeBases ?? new List<AiKnowledgeBaseOptions>())
                .Where(b => b != null && b.Enabled)
                .Take(MaxBases)
                .ToList();

            var result = new List<AiKnowledgeBaseOptions>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < configured.Count; i++)
            {
                var item = configured[i];
                var id = string.IsNullOrWhiteSpace(item.Id) ? $"kb{i + 1}" : item.Id.Trim();
                // 重复 Id 只保留第一个：模型按 Id 选库，重名会导致目标不唯一
                if (!usedIds.Add(id)) continue;

                item.Id = id;
                item.Name = string.IsNullOrWhiteSpace(item.Name) ? id : item.Name.Trim();
                item.Provider = string.IsNullOrWhiteSpace(item.Provider) ? GenericProvider : item.Provider.Trim().ToLowerInvariant();
                item.Endpoint = item.Endpoint?.Trim();
                item.ApiKey = item.ApiKey?.Trim();
                item.DatasetId = item.DatasetId?.Trim();
                item.KbId = item.KbId?.Trim();
                item.TopK = Math.Clamp(item.TopK, 1, 20);
                item.MinScore = Math.Clamp(item.MinScore, 0, 1);
                item.TimeoutSeconds = Math.Clamp(item.TimeoutSeconds, 1, 120);
                // 库级权限码为空 = 不做库级限制（工具级权限在下发工具时统一校验）
                item.Permission = string.IsNullOrWhiteSpace(item.Permission) ? null : item.Permission.Trim();
                result.Add(item);
            }

            if (result.Count == 0)
            {
                return result;
            }

            var defaultIndex = result.FindIndex(b => b.IsDefault);
            if (defaultIndex < 0) defaultIndex = 0;
            for (var i = 0; i < result.Count; i++)
            {
                result[i].IsDefault = i == defaultIndex;
            }
            // 默认库排第一：工具描述与前端选择器的首项即"不指定时用哪个"
            if (defaultIndex > 0)
            {
                var defaultBase = result[defaultIndex];
                result.RemoveAt(defaultIndex);
                result.Insert(0, defaultBase);
            }
            return result;
        }

        /// <summary>生效库数量上限：防配置膨胀后工具描述过长、模型选择困难。</summary>
        public const int MaxBases = 20;

        /// <summary>generic 渠道名常量</summary>
        public const string GenericProvider = "generic";

        /// <summary>Dify 渠道名常量</summary>
        public const string DifyProvider = "dify";

        /// <summary>把全局配置收敛到安全范围（库级数值在 <see cref="BuildBases"/> 里收敛）。</summary>
        public AiKnowledgeOptions Normalize()
        {
            MaxContentChars = Math.Clamp(MaxContentChars <= 0 ? 800 : MaxContentChars, 200, 4000);
            Permission = string.IsNullOrWhiteSpace(Permission) ? null : Permission.Trim();
            return this;
        }
    }

    /// <summary>
    /// 单个知识库配置：连接信息与检索参数全部自带，缺省项用下方默认值，不继承任何顶层字段。
    /// </summary>
    public class AiKnowledgeBaseOptions
    {
        /// <summary>库标识（模型通过 kbId 指定、审计记录用）；留空自动补 kb1/kb2…，生产配置建议显式填写。</summary>
        public string Id { get; set; }

        /// <summary>展示名（回灌文本的"来源"，也是前端选择器的显示值）；留空用 Id</summary>
        public string Name { get; set; }

        /// <summary>是否启用；默认启用，下线一个库只需把 Enabled 改为 false</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>给模型看的一句话用途，如"公司制度、报销规范"；留空则只显示库名</summary>
        public string Description { get; set; }

        /// <summary>是否默认库：模型未指定 kbId 时使用。都未标记时取配置中的第一个。</summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// 渠道名：generic = 自建 HTTP 检索接口；dify = Dify 数据集检索。
        /// 新增渠道时实现 <see cref="IKnowledgeProvider"/> 并标注 [AppService] 即可，无需改本插件。
        /// </summary>
        public string Provider { get; set; } = AiKnowledgeOptions.GenericProvider;

        /// <summary>
        /// 接口地址（必填）。
        /// <para>generic：完整检索 URL，如 https://kb.example.com/api/search。</para>
        /// <para>dify：API 根地址，如 https://api.dify.ai/v1（实际请求 /datasets/{DatasetId}/retrieve）。</para>
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>访问密钥（Bearer）。留空表示不传 Authorization 头。</summary>
        public string ApiKey { get; set; }

        /// <summary>Dify 数据集 ID（Provider=dify 时必填）。</summary>
        public string DatasetId { get; set; }

        /// <summary>上游库标识（generic 原样透传给检索接口，可留空）。</summary>
        public string KbId { get; set; }

        /// <summary>默认返回条数（1-20，模型可传 topK 覆盖）。</summary>
        public int TopK { get; set; } = 5;

        /// <summary>相似度下限（0-1，0 表示不启用）。低于该值的片段会被丢弃。</summary>
        public double MinScore { get; set; }

        /// <summary>单次检索超时（秒，1-120）。超时返回友好提示，不重试（检索是只读短请求，重试收益低）。</summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>库级权限码；空=不做库级限制（工具级权限见 AiKnowledge:Permission）</summary>
        public string Permission { get; set; }
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
