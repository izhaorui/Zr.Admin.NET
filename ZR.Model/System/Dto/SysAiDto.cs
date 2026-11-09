namespace ZR.Model.System.Dto
{
    /// <summary>
    /// AI 多语言翻译：单条待翻译条目
    /// </summary>
    public class SysAiLangItem
    {
        /// <summary>文案键（对应 sys_common_lang.lang_key）</summary>
        public string LangKey { get; set; }

        /// <summary>源语言文案</summary>
        public string Text { get; set; }

        /// <summary>可选语境，如「菜单名」「按钮文案」「错误提示」，用于消歧</summary>
        public string Context { get; set; }
    }

    /// <summary>
    /// AI 多语言翻译入参
    /// </summary>
    public class SysAiLangTranslateInput
    {
        /// <summary>源语言代码，如 zh-cn</summary>
        public string SourceLang { get; set; }

        /// <summary>目标语言代码列表，如 ["en","zh-tw"]</summary>
        public List<string> TargetLangs { get; set; }

        /// <summary>待翻译条目</summary>
        public List<SysAiLangItem> Items { get; set; }
    }

    /// <summary>
    /// 单条译文
    /// </summary>
    public class SysAiLangTranslation
    {
        public string LangCode { get; set; }
        public string LangName { get; set; }
    }

    /// <summary>
    /// 单个文案键的翻译结果（含全部目标语言）
    /// </summary>
    public class SysAiLangEntry
    {
        public string LangKey { get; set; }

        /// <summary>源语言文案，便于前端对照展示</summary>
        public string Text { get; set; }

        public List<SysAiLangTranslation> Translations { get; set; } = new();
    }

    /// <summary>
    /// AI 多语言翻译结果（草稿，未落库）
    /// </summary>
    public class SysAiLangTranslateResult
    {
        public string SourceLang { get; set; }

        public List<string> TargetLangs { get; set; } = new();

        public List<SysAiLangEntry> Entries { get; set; } = new();

        /// <summary>AI 返回非结构化 JSON 时承载原文，便于前端排查与降级展示</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// AI 译文落库入参
    /// </summary>
    public class SysAiLangApplyInput
    {
        /// <summary>前端可编辑后的译文条目</summary>
        public List<SysAiLangEntry> Entries { get; set; }

        /// <summary>true 覆盖已存在的译文，false 只补空缺（默认 false，避免误改存量）</summary>
        public bool Cover { get; set; }
    }

    /// <summary>
    /// AI 自然语言转 Cron 入参
    /// </summary>
    public class SysAiCronParseInput
    {
        /// <summary>自然语言调度描述，如「每天凌晨2点执行」</summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// AI 自然语言转 Cron 结果
    /// </summary>
    public class SysAiCronParseResult
    {
        /// <summary>Quartz 格式 Cron 表达式</summary>
        public string Cron { get; set; }

        /// <summary>执行规律的中文说明，供用户核对</summary>
        public string Description { get; set; }

        /// <summary>AI 对歧义描述所做的假设说明，无则为空</summary>
        public string Warnings { get; set; }

        /// <summary>由服务端按 Quartz 本地计算的下几次执行时间，非 AI 生成</summary>
        public List<string> NextTimes { get; set; } = new();

        /// <summary>AI 返回非结构化 JSON 时承载原文</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// AI 一句话解析日程入参
    /// </summary>
    public class SysAiScheduleParseInput
    {
        /// <summary>口语化描述，如「下周三下午三点前把季度报表发给张总，加急」</summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// AI 一句话解析日程结果（草稿，需用户确认后才落库）
    /// </summary>
    public class SysAiScheduleParseResult
    {
        public string Title { get; set; }

        public string Content { get; set; }

        /// <summary>优先级：1低 2中 3高</summary>
        public int Priority { get; set; } = 2;

        /// <summary>截止时间，格式 yyyy-MM-dd HH:mm，无则为空</summary>
        public string DueTime { get; set; }

        /// <summary>提醒时间，格式 yyyy-MM-dd HH:mm，无则为空</summary>
        public string ReminderTime { get; set; }

        /// <summary>AI 对模糊表述所做的假设说明</summary>
        public string Warnings { get; set; }

        /// <summary>AI 返回非结构化 JSON 时承载原文</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// AI 周报汇总入参（不传时间则默认取本周，周一为起始日）
    /// </summary>
    public class SysAiWeeklyReportInput
    {
        public DateTime? BeginTime { get; set; }

        public DateTime? EndTime { get; set; }
    }

    /// <summary>
    /// AI 周报汇总结果（纯文本，不落库）
    /// </summary>
    public class SysAiWeeklyReportResult
    {
        public string PeriodStart { get; set; }

        public string PeriodEnd { get; set; }

        /// <summary>整体情况概述</summary>
        public string Summary { get; set; }

        /// <summary>已完成事项</summary>
        public List<string> Completed { get; set; } = new();

        /// <summary>未完成事项</summary>
        public List<string> Pending { get; set; } = new();

        /// <summary>可观察到的风险项</summary>
        public List<string> Risks { get; set; } = new();

        /// <summary>参与统计的日程总数，便于用户核对是否与页面一致</summary>
        public int Total { get; set; }

        /// <summary>AI 返回非结构化 JSON 时承载原文</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// 代码生成列配置推断：单列输入描述
    /// </summary>
    public class SysAiGenColumnItem
    {
        /// <summary>数据库列名</summary>
        public string ColumnName { get; set; }

        public string CsharpType { get; set; }

        /// <summary>列长度上限，0 表示未取到</summary>
        public int Length { get; set; }

        public bool IsPk { get; set; }

        public bool IsNullable { get; set; }

        /// <summary>数据库列注释，可能为空</summary>
        public string Comment { get; set; }
    }

    /// <summary>
    /// 代码生成列配置推断入参
    /// </summary>
    public class SysAiGenColumnInput
    {
        public string TableName { get; set; }

        public List<SysAiGenColumnItem> Columns { get; set; }
    }

    /// <summary>
    /// 单列配置建议。Current* 为库里当前值，便于前端做差异对比后让用户勾选。
    /// </summary>
    public class SysAiGenColumnSuggestion
    {
        public string ColumnName { get; set; }

        /// <summary>建议的中文标签</summary>
        public string Comment { get; set; }

        /// <summary>建议的控件类型</summary>
        public string HtmlType { get; set; }

        public bool IsRequired { get; set; }

        public bool IsList { get; set; }

        public bool IsQuery { get; set; }

        public bool IsEdit { get; set; }

        /// <summary>推断依据，供人工复核</summary>
        public string Reason { get; set; }

        /// <summary>库中当前的注释</summary>
        public string CurrentComment { get; set; }

        /// <summary>库中当前的控件类型</summary>
        public string CurrentHtmlType { get; set; }

        /// <summary>建议与当前值存在差异的字段，便于前端高亮，如 "comment,htmlType"</summary>
        public string DiffFields { get; set; }

        /// <summary>true 时前端默认不勾选，避免用户误采纳改坏结构约束</summary>
        public bool SkipByDefault { get; set; }
    }

    /// <summary>
    /// 代码生成列配置推断结果（纯建议，不落库）
    /// </summary>
    public class SysAiGenColumnResult
    {
        public string TableName { get; set; }

        public List<SysAiGenColumnSuggestion> Columns { get; set; } = new();

        /// <summary>AI 返回非结构化 JSON 时承载原文</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// 代码生成表级配置建议项。值统一用字符串承载，便于前端统一渲染与差异对比。
    /// </summary>
    public class SysAiGenFieldSuggestion
    {
        /// <summary>字段标识，与采纳接口入参一致，如 tplCategory / checkedBtn</summary>
        public string Field { get; set; }

        /// <summary>中文名，用于界面展示</summary>
        public string Label { get; set; }

        /// <summary>库中当前值，布尔用 true/false、按钮用 1,2,3</summary>
        public string CurrentValue { get; set; }

        /// <summary>建议值</summary>
        public string SuggestValue { get; set; }

        /// <summary>推断依据，供人工复核</summary>
        public string Reason { get; set; }

        /// <summary>true 时前端默认不勾选（与当前值相同或置信度低）</summary>
        public bool SkipByDefault { get; set; }
    }

    /// <summary>
    /// 代码生成整表配置建议结果（纯建议，不落库）
    /// </summary>
    public class SysAiGenTableResult
    {
        public string TableName { get; set; }

        public List<SysAiGenFieldSuggestion> Fields { get; set; } = new();

        /// <summary>AI 返回非结构化 JSON 时承载原文</summary>
        public string Raw { get; set; }
    }

    /// <summary>
    /// 代码生成单列采纳项。为 null 的字段表示不采纳，未提交的列保持原配置。
    /// </summary>
    public class SysAiGenColumnApply
    {
        /// <summary>数据库列名（对应 gen_table_column.column_name）</summary>
        public string ColumnName { get; set; }

        /// <summary>中文标签</summary>
        public string Comment { get; set; }

        /// <summary>控件类型，须在服务端白名单内</summary>
        public string HtmlType { get; set; }

        public bool? IsRequired { get; set; }

        public bool? IsList { get; set; }

        public bool? IsQuery { get; set; }

        public bool? IsEdit { get; set; }
    }

    /// <summary>
    /// 采纳 AI 建议入参。字段/属性为 null 表示不采纳，前端只提交用户勾选项。
    /// </summary>
    public class SysAiGenApplyInput
    {
        public string TableComment { get; set; }

        public string FunctionName { get; set; }

        /// <summary>业务域模块名，如 system / shop，不能与 BusinessName 相同</summary>
        public string ModuleName { get; set; }

        /// <summary>业务名，如 Order</summary>
        public string BusinessName { get; set; }

        /// <summary>模板类型：crud/tree/sub/subNav/subNavMore</summary>
        public string TplCategory { get; set; }

        /// <summary>权限前缀，小写字母开头，如 order</summary>
        public string PermissionPrefix { get; set; }

        /// <summary>是否启用在服务层记录数据变更日志</summary>
        public bool? EnableLog { get; set; }

        /// <summary>按钮集合：1新增 2修改 3删除 4导出 5查看 6清空 7批量删除 8导入</summary>
        public int[] CheckedBtn { get; set; }

        public List<SysAiGenColumnApply> Columns { get; set; }
    }

    /// <summary>
    /// 采纳 AI 建议的结果
    /// </summary>
    public class SysAiGenApplyResult
    {
        /// <summary>实际更新的表级字段数</summary>
        public int UpdatedFields { get; set; }

        /// <summary>实际更新的列数</summary>
        public int UpdatedColumns { get; set; }
    }
}
