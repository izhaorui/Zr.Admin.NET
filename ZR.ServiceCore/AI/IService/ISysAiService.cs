using ZR.Model.System.Dto;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// 系统模块 AI 能力：多语言批量翻译、自然语言生成 Cron 表达式。
    /// 复用工作流同款 AI 基建（AiLlmClient + PromptLoader + AiOptions），提示词见 Prompts/system/。
    /// </summary>
    public interface ISysAiService
    {
        /// <summary>
        /// AI 批量翻译界面文案，返回可编辑草稿，不落库
        /// </summary>
        Task<SysAiLangTranslateResult> TranslateLangAsync(SysAiLangTranslateInput input);

        /// <summary>
        /// 把译文写入 sys_common_lang。Cover=false 时只补空缺，不覆盖存量
        /// </summary>
        (string, object, object) ApplyLangTranslation(SysAiLangApplyInput input);

        /// <summary>
        /// 自然语言调度描述转 Quartz Cron 表达式
        /// </summary>
        Task<SysAiCronParseResult> ParseCronAsync(SysAiCronParseInput input);

        /// <summary>
        /// 一句话口语描述解析为日程字段，返回可编辑草稿，不落库
        /// </summary>
        Task<SysAiScheduleParseResult> ParseScheduleAsync(SysAiScheduleParseInput input);

        /// <summary>
        /// 汇总指定区间（默认本周）内当前用户的日程生成周报文本，不落库
        /// </summary>
        Task<SysAiWeeklyReportResult> GenerateWeeklyReportAsync(SysAiWeeklyReportInput input, long userId);

        /// <summary>
        /// 按代码生成表 id 推断各列配置建议（中文标签/控件类型/是否列表查询等）。
        /// 只返回建议不落库，采纳与否由用户在列配置页勾选后走既有保存接口。
        /// </summary>
        Task<SysAiGenColumnResult> SuggestGenColumnsAsync(long tableId);

        /// <summary>
        /// 按代码生成表 id 推断表级配置（模板类型/模块名/业务名/权限前缀/按钮等）。
        /// 只返回建议不落库，采纳与否由用户勾选后走 ApplyGenSuggestion。
        /// </summary>
        Task<SysAiGenTableResult> SuggestGenTableAsync(long tableId);

        /// <summary>
        /// 采纳 AI 建议写回 gen_table / gen_table_column。
        /// 服务端重新校验全部取值（不信任前端提交内容），整体走事务，任一失败全部回滚。
        /// </summary>
        /// <param name="tableId">代码生成表 id</param>
        /// <param name="input">用户勾选采纳的字段与列</param>
        /// <param name="userName">操作人，写入 Update_by</param>
        SysAiGenApplyResult ApplyGenSuggestion(long tableId, SysAiGenApplyInput input, string userName);

        /// <summary>
        /// 解读登录日志聚合指标生成安全分析报告（Markdown，不落库）
        /// </summary>
        Task<SysAiLogReportResult> AnalyzeLoginSecurityAsync(LoginSecurityMetricsDto metrics);

        /// <summary>
        /// 解读操作日志聚合指标生成健康分析报告（Markdown，不落库）
        /// </summary>
        Task<SysAiLogReportResult> AnalyzeOperHealthAsync(OperHealthMetricsDto metrics);
    }
}
