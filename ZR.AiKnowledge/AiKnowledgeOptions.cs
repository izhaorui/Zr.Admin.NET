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
                    var options = resolver != null
                        ? resolver()
                        : AppSettings.Bind(SectionName, new AiKnowledgeOptions());
                    return (options ?? new AiKnowledgeOptions()).Normalize();
                }
                catch (Exception ex)
                {
                    // 配置文件缺失/格式错误时按未启用处理：知识库是可选能力，不能影响主流程
                    Logger.Warn(ex, "读取知识库配置失败，按未启用处理");
                    return new AiKnowledgeOptions();
                }
            }
        }
    }
}
