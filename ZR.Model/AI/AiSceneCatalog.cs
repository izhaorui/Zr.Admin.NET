namespace ZR.Model.AI
{
    /// <summary>
    /// AI 调用场景目录：治理策略（额度/限流/审计）的匹配键，也是各调用点传参的取值来源。
    /// 调用点一律引用本类常量，不再手写字符串——避免场景改名时漏改、或多处写成不同拼写导致
    /// 策略匹配不上（策略是按场景精确匹配的，拼错不会报错，只会静默不生效）。
    /// 新增场景须同步三处：① 在此声明常量 ② 加入 <see cref="All"/> ③ 调用点使用该常量。
    /// </summary>
    public static class AiSceneCatalog
    {
        /// <summary>对话助手。唯一启用分钟限流（ChatRateLimitPerMinute）与默认兜底额度的场景。</summary>
        public const string AiChat = "ai_chat";

        /// <summary>界面文案批量翻译（sys_common_lang 草稿）</summary>
        public const string LangTranslate = "lang_translate";

        /// <summary>自然语言生成 Quartz Cron 表达式</summary>
        public const string CronParse = "cron_parse";

        /// <summary>口语化描述解析为日程字段</summary>
        public const string ScheduleParse = "schedule_parse";

        /// <summary>日程汇总生成周报草稿</summary>
        public const string WeeklyReport = "weekly_report";

        /// <summary>代码生成器列配置推断</summary>
        public const string GenColumns = "gen_columns";

        /// <summary>登录日志安全分析报告</summary>
        public const string LoginSecurity = "login_security";

        /// <summary>操作日志健康分析报告</summary>
        public const string OperHealth = "oper_health";

        /// <summary>工作流：自然语言生成流程草稿</summary>
        public const string WfGenerate = "wf_generate";

        /// <summary>工作流：审批意见话术建议（支持图片附件）</summary>
        public const string WfApprovalSuggest = "wf_approval_suggest";

        /// <summary>工作流：流程优化体检</summary>
        public const string WfFlowOptimize = "wf_flow_optimize";

        /// <summary>工作流：自然语言匹配流程并预填表单</summary>
        public const string WfIntentMatch = "wf_intent_match";

        /// <summary>工作流：流程实例审批链摘要</summary>
        public const string WfInstanceSummary = "wf_instance_summary";

        /// <summary>工作流：审批风险预判（支持图片附件）</summary>
        public const string WfRiskCheck = "wf_risk_check";

        /// <summary>工作流：审批记录摘要</summary>
        public const string WfApprovalSummary = "wf_approval_summary";

        /// <summary>商城：商品营销文案生成（标题/卖点/详情描述/SEO 关键词）</summary>
        public const string ShopProductCopy = "shop_product_copy";

        /// <summary>治理自检：Provider 连通性探测</summary>
        public const string HealthCheck = "health_check";

        /// <summary>
        /// 策略可填的场景全集（含通配 "*"，表示对全部场景生效），供策略保存时做白名单校验。
        /// </summary>
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "*",
            AiChat,
            LangTranslate,
            CronParse,
            ScheduleParse,
            WeeklyReport,
            GenColumns,
            LoginSecurity,
            OperHealth,
            WfGenerate,
            WfApprovalSuggest,
            WfFlowOptimize,
            WfIntentMatch,
            WfInstanceSummary,
            WfRiskCheck,
            WfApprovalSummary,
            ShopProductCopy,
            HealthCheck
        };
    }
}
