using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Helper;
using System.Globalization;
using System.Text.Json;
using ZR.Model.AI;
using ZR.Model.Models;
using ZR.Model.System.Dto;
using ZR.Model.System.Generate;
using ZR.ServiceCore.AI.IService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.AI
{
    /// <summary>
    /// 系统模块 AI 能力：多语言批量翻译、自然语言生成 Cron 表达式。
    /// 与工作流 AI 共用同一套基建（AiLlmClient / PromptLoader / AiOptions），
    /// 提示词放在 Prompts/system/ 下，可由运维热改无需重编译。
    /// </summary>
    [AppService(ServiceType = typeof(ISysAiService), ServiceLifetime = LifeTime.Transient)]
    public class SysAiService : ISysAiService
    {
        /// <summary>单次翻译条目上限，防止超长请求撑爆上下文与 token</summary>
        private const int MaxTranslateItems = 50;

        /// <summary>单条源文案长度上限</summary>
        private const int MaxItemTextLength = 1000;

        /// <summary>语言代码长度上限，对齐 sys_common_lang.lang_code 列长</summary>
        private const int LangCodeMaxLength = 10;

        /// <summary>周报允许参与汇总的日程上限，超出只取前 N 条，避免撑爆上下文</summary>
        private const int MaxWeeklyReportItems = 100;

        /// <summary>单次推断列数上限，超宽表需分批</summary>
        private const int MaxGenColumnItems = 80;

        /// <summary>
        /// AI 返回的单个列建议。用于在 JsonDocument 释放前把数据取出来，
        /// JsonElement 只是文档内游标，不能跨 using 持有。
        /// </summary>
        private sealed class GenColumnReply
        {
            public string Comment { get; set; }
            public string Reason { get; set; }
            public string HtmlType { get; set; }
            public bool IsRequired { get; set; }
            public bool IsList { get; set; }
            public bool IsQuery { get; set; }
            public bool IsEdit { get; set; }
        }

        /// <summary>
        /// 代码生成允许的控件类型白名单。AI 只能从中取值，越界一律回落 input，
        /// 否则未知类型会让代码模板渲染出无法编译的前端代码。
        /// </summary>
        private static readonly HashSet<string> AllowedHtmlTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "input", "inputNumber", "textarea", "select", "selectMulti", "radio",
            "checkbox", "datetime", "imageUpload", "fileUpload", "editor",
            "customInput", "colorPicker"
        };

        /// <summary>指标序列化选项：camelCase，让提示词里的字段说明与 JSON 字段名一致</summary>
        private static readonly JsonSerializerOptions MetricJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ICommonLangService _commonLangService;
        private readonly IDailyScheduleService _dailyScheduleService;
        private readonly IGenTableColumnService _genTableColumnService;

        public SysAiService(
            ICommonLangService commonLangService,
            IDailyScheduleService dailyScheduleService,
            IGenTableColumnService genTableColumnService)
        {
            _commonLangService = commonLangService;
            _dailyScheduleService = dailyScheduleService;
            _genTableColumnService = genTableColumnService;
        }

        public async Task<SysAiLangTranslateResult> TranslateLangAsync(SysAiLangTranslateInput input)
        {
            if (input == null)
            {
                throw new CustomException("请求参数不能为空");
            }

            var sourceLang = NormalizeLangCode(input.SourceLang) ?? "zh-cn";
            var targetLangs = (input.TargetLangs ?? new List<string>())
                .Select(NormalizeLangCode)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targetLangs.Count == 0)
            {
                throw new CustomException("请至少选择一个目标语言");
            }

            // 目标语言与源语言相同时没有翻译意义，直接剔除，避免浪费 token
            targetLangs.RemoveAll(x => string.Equals(x, sourceLang, StringComparison.OrdinalIgnoreCase));
            if (targetLangs.Count == 0)
            {
                throw new CustomException("目标语言与源语言相同，无需翻译");
            }

            var items = (input.Items ?? new List<SysAiLangItem>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.LangKey) && !string.IsNullOrWhiteSpace(x.Text))
                .ToList();
            if (items.Count == 0)
            {
                throw new CustomException("没有可翻译的文案，请先选择需要翻译的条目");
            }
            if (items.Count > MaxTranslateItems)
            {
                throw new CustomException($"单次最多翻译 {MaxTranslateItems} 条文案，当前 {items.Count} 条，请分批操作");
            }

            var user = BuildTranslatePrompt(sourceLang, targetLangs, items);
            var text = await AiHelper.ChatSafeAsync(AiHelper.GetPromptOrThrow("system/lang-translate.md", "多语言翻译"), user, AiSceneCatalog.LangTranslate).ConfigureAwait(false);

            return ParseTranslateResult(text, sourceLang, targetLangs, items);
        }

        public (string, object, object) ApplyLangTranslation(SysAiLangApplyInput input)
        {
            var entries = input?.Entries ?? new List<SysAiLangEntry>();
            var list = new List<CommonLang>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.Now;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LangKey)) continue;
                var langKey = AiHelper.Truncate(entry.LangKey.Trim(), 100);

                foreach (var t in entry.Translations ?? new List<SysAiLangTranslation>())
                {
                    if (t == null) continue;
                    var langCode = NormalizeLangCode(t.LangCode);
                    if (string.IsNullOrEmpty(langCode) || string.IsNullOrWhiteSpace(t.LangName)) continue;
                    // 同一文案键 + 语言只保留首个，避免重复条目让 Storageable 走批量更新产生歧义
                    if (!seen.Add($"{langKey}|{langCode}")) continue;

                    list.Add(new CommonLang
                    {
                        LangKey = langKey,
                        LangCode = langCode,
                        LangName = AiHelper.Truncate(t.LangName.Trim(), 2000),
                        Addtime = now
                    });
                }
            }

            if (list.Count == 0)
            {
                throw new CustomException("没有可应用的译文，请检查译文内容是否为空");
            }

            if (input != null && !input.Cover)
            {
                list = FilterExisting(list);
                if (list.Count == 0)
                {
                    throw new CustomException("所选文案的目标语言译文均已存在（未勾选覆盖），没有需要新增的内容");
                }
            }

            return _commonLangService.ImportCommonLang(list);
        }

        public async Task<SysAiCronParseResult> ParseCronAsync(SysAiCronParseInput input)
        {
            var text = input?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new CustomException("请填写调度描述，例如「每天凌晨2点执行」");
            }
            if (text.Length > 200)
            {
                throw new CustomException("调度描述过长，请精简到 200 字以内");
            }

            var reply = await AiHelper.ChatSafeAsync(AiHelper.GetPromptOrThrow("system/cron-parse.md", "Cron 表达式生成"), $"调度描述：{text}", AiSceneCatalog.CronParse).ConfigureAwait(false);
            return ParseCronResult(reply);
        }

        public async Task<SysAiScheduleParseResult> ParseScheduleAsync(SysAiScheduleParseInput input)
        {
            var text = input?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new CustomException("请先描述日程内容，例如「下周三下午三点前把季度报表发给张总，加急」");
            }
            if (text.Length > 500)
            {
                throw new CustomException("描述过长，请精简到 500 字以内");
            }

            // 相对时间（明天/下周三）只有服务端知道基准，必须注入，否则 AI 会按自己的日期认知乱算
            var now = DateTime.Now;
            var user = $"currentTime：{now:yyyy-MM-dd HH:mm}（星期{GetCnWeekday(now.DayOfWeek)}）\nweekStart：1\ntext：{text}";

            var reply = await AiHelper.ChatSafeAsync(AiHelper.GetPromptOrThrow("system/schedule-parse.md", "日程解析"), user, AiSceneCatalog.ScheduleParse).ConfigureAwait(false);
            return ParseScheduleResult(reply);
        }

        public async Task<SysAiWeeklyReportResult> GenerateWeeklyReportAsync(SysAiWeeklyReportInput input, long userId)
        {
            var (begin, end) = ResolveReportRange(input);
            var periodStart = begin.ToString("yyyy-MM-dd");
            var periodEnd = end.ToString("yyyy-MM-dd");

            var schedules = await _dailyScheduleService.GetByDateRangeAsync(userId, begin, end).ConfigureAwait(false);
            var total = schedules.Count;

            if (total == 0)
            {
                return new SysAiWeeklyReportResult
                {
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Total = 0,
                    Summary = "本期无日程记录，无需生成周报。"
                };
            }

            var truncated = total > MaxWeeklyReportItems;
            if (truncated)
            {
                schedules = schedules.Take(MaxWeeklyReportItems).ToList();
            }

            var payload = new
            {
                periodStart,
                periodEnd,
                schedules = schedules.Select(x => new
                {
                    title = AiHelper.Truncate(x.Title, 100),
                    content = AiHelper.Truncate(x.Content ?? string.Empty, 500),
                    status = x.Status,
                    priority = x.Priority,
                    dueTime = x.DueTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty
                }).ToList()
            };

            var user = System.Text.Json.JsonSerializer.Serialize(payload);
            if (truncated)
            {
                user += $"\n（日程较多，仅提供前 {MaxWeeklyReportItems} 条，汇总时请说明数据已截断）";
            }

            var reply = await AiHelper.ChatSafeAsync(AiHelper.GetPromptOrThrow("system/schedule-weekly-report.md", "周报汇总"), user, AiSceneCatalog.WeeklyReport).ConfigureAwait(false);
            var parsed = ParseWeeklyReportResult(reply);
            parsed.PeriodStart = periodStart;
            parsed.PeriodEnd = periodEnd;
            parsed.Total = total;
            return parsed;
        }

        public async Task<SysAiGenColumnResult> SuggestGenColumnsAsync(long tableId)
        {
            if (tableId <= 0)
            {
                throw new CustomException("参数错误：tableId 不能为空");
            }

            var dbColumns = _genTableColumnService.GenTableColumns(tableId)
                .Where(x => !string.IsNullOrWhiteSpace(x.ColumnName))
                .ToList();
            if (dbColumns.Count == 0)
            {
                throw new CustomException("没有可推断的列，请确认表结构已导入");
            }
            if (dbColumns.Count > MaxGenColumnItems)
            {
                throw new CustomException($"单次最多推断 {MaxGenColumnItems} 列，当前 {dbColumns.Count} 列，请拆分表后重试");
            }

            var payload = new
            {
                tableName = dbColumns.FirstOrDefault()?.TableName ?? string.Empty,
                columns = dbColumns.Select(x => new
                {
                    columnName = AiHelper.Truncate(x.ColumnName.Trim(), 100),
                    csharpType = x.CsharpType ?? string.Empty,
                    // GenTableColumn 未单独存长度，只能从 ColumnType（如 nvarchar(500)）里解析
                    length = ParseColumnLength(x.ColumnType),
                    isPk = x.IsPk,
                    isNullable = !x.IsRequired,
                    comment = AiHelper.Truncate(x.ColumnComment ?? string.Empty, 200)
                }).ToList()
            };

            var reply = await AiHelper.ChatSafeAsync(
                AiHelper.GetPromptOrThrow("system/gencode-columns.md", "代码生成列配置推断"),
                System.Text.Json.JsonSerializer.Serialize(payload), AiSceneCatalog.GenColumns).ConfigureAwait(false);

            return ParseGenColumnResult(reply, payload.tableName, dbColumns);
        }

        /// <summary>
        /// 解读登录日志聚合指标，生成 Markdown 安全分析报告（不落库）。
        /// 指标由服务端固定 SQL 聚合，模型只负责解读，不接触原始日志。
        /// </summary>
        public Task<SysAiLogReportResult> AnalyzeLoginSecurityAsync(LoginSecurityMetricsDto metrics)
        {
            return AnalyzeMetricsAsync(metrics, "system/log-login-analysis.md", "登录日志 AI 安全分析", AiSceneCatalog.LoginSecurity);
        }

        /// <summary>
        /// 解读操作日志聚合指标（错误已聚类），生成 Markdown 健康分析报告（不落库）。
        /// </summary>
        public Task<SysAiLogReportResult> AnalyzeOperHealthAsync(OperHealthMetricsDto metrics)
        {
            return AnalyzeMetricsAsync(metrics, "system/log-oper-analysis.md", "操作日志 AI 健康分析", AiSceneCatalog.OperHealth);
        }

        /// <summary>
        /// 通用指标解读：序列化指标后调 LLM，返回 Markdown 报告。
        /// </summary>
        private static async Task<SysAiLogReportResult> AnalyzeMetricsAsync<T>(T metrics, string promptPath, string promptName, string scene)
        {
            if (metrics == null)
            {
                throw new CustomException("聚合指标不能为空");
            }

            var reply = await AiHelper.ChatSafeAsync(
                AiHelper.GetPromptOrThrow(promptPath, promptName),
                System.Text.Json.JsonSerializer.Serialize(metrics, MetricJsonOptions), scene).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(reply))
            {
                throw new CustomException("AI 未返回分析报告，请稍后重试");
            }
            return new SysAiLogReportResult { Report = reply.Trim() };
        }

        /// <summary>
        /// 组装翻译请求正文：用 JSON 承载条目，避免多行文本在换行/引号上与提示词混淆。
        /// </summary>
        private static string BuildTranslatePrompt(string sourceLang, List<string> targetLangs, List<SysAiLangItem> items)
        {
            var payload = new
            {
                sourceLang,
                targetLangs,
                items = items.Select(x => new
                {
                    langKey = AiHelper.Truncate(x.LangKey.Trim(), 100),
                    text = AiHelper.Truncate(x.Text.Trim(), MaxItemTextLength),
                    context = string.IsNullOrWhiteSpace(x.Context) ? string.Empty : AiHelper.Truncate(x.Context.Trim(), 100)
                }).ToList()
            };
            return System.Text.Json.JsonSerializer.Serialize(payload);
        }

        /// <summary>
        /// 解析翻译结果。按 langKey 与 AI 输出对齐，缺失的条目译文留空由前端提示，绝不串行错位。
        /// </summary>
        private static SysAiLangTranslateResult ParseTranslateResult(string raw, string sourceLang, List<string> targetLangs, List<SysAiLangItem> items)
        {
            var result = new SysAiLangTranslateResult { SourceLang = sourceLang, TargetLangs = targetLangs };

            var byKey = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var parsed = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                var json = JsonHelper.StripMarkdown(raw);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("entries", out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            if (!el.TryGetProperty("langKey", out var keyEl)) continue;
                            var key = keyEl.GetString();
                            if (string.IsNullOrWhiteSpace(key)) continue;

                            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            if (el.TryGetProperty("translations", out var ts) && ts.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var t in ts.EnumerateArray())
                                {
                                    var code = NormalizeLangCode(ReadString(t, "langCode"));
                                    var name = ReadString(t, "langName");
                                    if (string.IsNullOrEmpty(code) || string.IsNullOrWhiteSpace(name)) continue;
                                    map[code] = name.Trim();
                                }
                            }
                            byKey[key.Trim()] = map;
                        }
                        parsed = true;
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // AI 返回非结构化文本时按原文兜底，交由前端展示并提示重试
                }
            }

            if (!parsed)
            {
                result.Raw = raw?.Trim();
                return result;
            }

            foreach (var item in items)
            {
                var key = item.LangKey.Trim();
                var entry = new SysAiLangEntry { LangKey = key, Text = item.Text.Trim() };
                byKey.TryGetValue(key, out var map);

                foreach (var lang in targetLangs)
                {
                    var name = map != null && map.TryGetValue(lang, out var v) ? v : string.Empty;
                    entry.Translations.Add(new SysAiLangTranslation { LangCode = lang, LangName = name });
                }
                result.Entries.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// 解析日程草稿。时间字段只接受 yyyy-MM-dd HH:mm，解析失败一律置空，
        /// 宁可让用户手填也不写入错误时间。
        /// </summary>
        private static SysAiScheduleParseResult ParseScheduleResult(string raw)
        {
            var result = new SysAiScheduleParseResult();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var json = JsonHelper.StripMarkdown(raw);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    result.Raw = raw.Trim();
                    return result;
                }

                result.Title = (ReadString(root, "title") ?? string.Empty).Trim();
                result.Content = (ReadString(root, "content") ?? string.Empty).Trim();
                result.Warnings = (ReadString(root, "warnings") ?? string.Empty).Trim();

                if (root.TryGetProperty("priority", out var priEl))
                {
                    if (priEl.ValueKind == JsonValueKind.Number && priEl.TryGetInt32(out var p) && p >= 1 && p <= 3)
                    {
                        result.Priority = p;
                    }
                }

                result.DueTime = ParseDateTimeText(ReadString(root, "dueTime"));
                result.ReminderTime = ParseDateTimeText(ReadString(root, "reminderTime"));
            }
            catch (System.Text.Json.JsonException)
            {
                result.Raw = raw.Trim();
            }
            return result;
        }

        /// <summary>
        /// 解析列配置建议。AI 输出一律过白名单与结构约束，并带上库中当前值供前端做差异对比。
        /// </summary>
        private static SysAiGenColumnResult ParseGenColumnResult(string raw, string tableName, List<GenTableColumn> columns)
        {
            var result = new SysAiGenColumnResult { TableName = tableName };
            // 必须在 JsonDocument 释放前把数据取出来：JsonElement 只是文档内的游标，
            // 文档一 dispose 再访问就抛 ObjectDisposedException
            var byName = new Dictionary<string, GenColumnReply>(StringComparer.OrdinalIgnoreCase);
            var parsed = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                var json = JsonHelper.StripMarkdown(raw);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("columns", out var arr) &&
                        arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            var name = ReadString(el, "columnName");
                            if (string.IsNullOrWhiteSpace(name) || el.ValueKind != JsonValueKind.Object) continue;
                            byName[name.Trim()] = new GenColumnReply
                            {
                                Comment = (ReadString(el, "comment") ?? string.Empty).Trim(),
                                Reason = (ReadString(el, "reason") ?? string.Empty).Trim(),
                                HtmlType = (ReadString(el, "htmlType") ?? string.Empty).Trim(),
                                IsRequired = ReadBool(el, "isRequired"),
                                IsList = ReadBool(el, "isList"),
                                IsQuery = ReadBool(el, "isQuery"),
                                IsEdit = ReadBool(el, "isEdit")
                            };
                        }
                        parsed = true;
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // 非结构化返回走 Raw 兜底
                }
            }

            if (!parsed)
            {
                result.Raw = raw?.Trim();
                return result;
            }

            foreach (var col in columns)
            {
                var name = col.ColumnName.Trim();
                byName.TryGetValue(name, out var reply);

                var currentComment = col.ColumnComment ?? string.Empty;
                var currentHtmlType = col.HtmlType ?? string.Empty;

                var suggestion = new SysAiGenColumnSuggestion
                {
                    ColumnName = name,
                    CurrentComment = currentComment,
                    CurrentHtmlType = currentHtmlType,
                    // 主键与自增列属于结构约束，默认不勾选避免误采纳改坏生成代码
                    SkipByDefault = col.IsPk
                };

                if (reply != null)
                {
                    suggestion.Comment = reply.Comment;
                    suggestion.Reason = reply.Reason;
                    suggestion.HtmlType = AllowedHtmlTypes.Contains(reply.HtmlType) ? reply.HtmlType : "input";
                    suggestion.IsRequired = reply.IsRequired;
                    suggestion.IsList = reply.IsList;
                    suggestion.IsQuery = reply.IsQuery;
                    suggestion.IsEdit = reply.IsEdit;
                }
                else
                {
                    // 该列 AI 未返回，给安全的兜底建议而不是留空
                    suggestion.Comment = currentComment;
                    suggestion.HtmlType = "input";
                    suggestion.IsRequired = col.IsRequired && !col.IsPk;
                    suggestion.IsList = col.IsList;
                    suggestion.IsQuery = col.IsQuery;
                    suggestion.IsEdit = col.IsEdit;
                    suggestion.Reason = "AI 未返回该列，已沿用当前配置";
                }

                // 数据库注释已有内容时以库里为准，不让 AI 改写人工录入或同步来的注释
                if (!string.IsNullOrWhiteSpace(currentComment))
                {
                    suggestion.Comment = currentComment;
                }
                // 主键不可编辑且非必填，强制收敛
                if (col.IsPk)
                {
                    suggestion.IsEdit = false;
                    suggestion.IsRequired = false;
                }

                suggestion.DiffFields = BuildDiffFields(suggestion, col);
                result.Columns.Add(suggestion);
            }
            return result;
        }

        /// <summary>
        /// 从列类型字符串里解析长度，如 "nvarchar(500)" 得 500。解析不到返回 0 表示未知。
        /// </summary>
        private static int ParseColumnLength(string columnType)
        {
            if (string.IsNullOrWhiteSpace(columnType)) return 0;

            var start = columnType.IndexOf('(');
            var end = columnType.IndexOf(')', start + 1);
            if (start < 0 || end <= start + 1) return 0;

            return int.TryParse(columnType.AsSpan(start + 1, end - start - 1), out var len) ? len : 0;
        }

        /// <summary>
        /// 计算建议与库中当前配置的差异字段，供前端高亮。
        /// 只比较本次会采纳的四项，注释与控件类型差异单独列出。
        /// </summary>
        private static string BuildDiffFields(SysAiGenColumnSuggestion s, GenTableColumn current)
        {
            var diffs = new List<string>();
            if (!string.Equals(s.Comment, current.ColumnComment ?? string.Empty, StringComparison.Ordinal))
            {
                diffs.Add("comment");
            }
            if (!string.Equals(s.HtmlType, current.HtmlType ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                diffs.Add("htmlType");
            }
            if (s.IsRequired != current.IsRequired) diffs.Add("isRequired");
            if (s.IsList != current.IsList) diffs.Add("isList");
            if (s.IsQuery != current.IsQuery) diffs.Add("isQuery");
            if (s.IsEdit != current.IsEdit) diffs.Add("isEdit");
            return string.Join(",", diffs);
        }

        private static bool ReadBool(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var v)) return false;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(v.GetString(), out var b) && b,
                JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
                _ => false
            };
        }

        private static SysAiWeeklyReportResult ParseWeeklyReportResult(string raw)
        {
            var result = new SysAiWeeklyReportResult();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var json = JsonHelper.StripMarkdown(raw);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    result.Raw = raw.Trim();
                    return result;
                }

                result.Summary = (ReadString(root, "summary") ?? string.Empty).Trim();
                result.Completed = ReadStringArray(root, "completed");
                result.Pending = ReadStringArray(root, "pending");
                result.Risks = ReadStringArray(root, "risks");
            }
            catch (System.Text.Json.JsonException)
            {
                result.Raw = raw.Trim();
            }
            return result;
        }

        /// <summary>
        /// 解析 AI 返回的时间文本。只认 yyyy-MM-dd HH:mm 与 yyyy-MM-dd 两种格式，
        /// 其它格式（含自然语言）一律返回空，避免脏时间入库。
        /// </summary>
        private static string ParseDateTimeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var text = value.Trim();

            string[] formats = { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" };
            if (!DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return string.Empty;
            }
            return dt.ToString("yyyy-MM-dd HH:mm");
        }

        private static List<string> ReadStringArray(JsonElement root, string name)
        {
            var list = new List<string>();
            if (!root.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;

            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String) continue;
                var v = el.GetString();
                if (!string.IsNullOrWhiteSpace(v)) list.Add(v.Trim());
            }
            return list;
        }

        /// <summary>
        /// 确定周报统计区间。未传时间默认取本周（周一 00:00 至周日 23:59:59）。
        /// 只传其一则另一边开放到当天；起止倒置时自动交换，避免查出空集让用户困惑。
        /// </summary>
        private static (DateTime, DateTime) ResolveReportRange(SysAiWeeklyReportInput input)
        {
            var begin = input?.BeginTime;
            var end = input?.EndTime;

            if (!begin.HasValue && !end.HasValue)
            {
                var today = DateTime.Today;
                // DayOfWeek: 周日=0，这里换算成以周一为一周起点的偏移
                var offset = today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1;
                var monday = today.AddDays(-offset);
                return (monday, monday.AddDays(7).AddSeconds(-1));
            }

            if (begin.HasValue && end.HasValue)
            {
                return begin.Value <= end.Value
                    ? (begin.Value, end.Value)
                    : (end.Value, begin.Value);
            }

            return begin.HasValue
                ? (begin.Value, DateTime.Today.AddDays(1).AddSeconds(-1))
                : (DateTime.Today.AddYears(-1), end.Value);
        }

        private static string GetCnWeekday(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Sunday => "日",
                DayOfWeek.Monday => "一",
                DayOfWeek.Tuesday => "二",
                DayOfWeek.Wednesday => "三",
                DayOfWeek.Thursday => "四",
                DayOfWeek.Friday => "五",
                _ => "六"
            };
        }

        private static SysAiCronParseResult ParseCronResult(string raw)
        {
            var result = new SysAiCronParseResult();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var json = JsonHelper.StripMarkdown(raw);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    result.Raw = raw.Trim();
                    return result;
                }
                result.Cron = (ReadString(root, "cron") ?? string.Empty).Trim();
                result.Description = (ReadString(root, "description") ?? string.Empty).Trim();
                result.Warnings = (ReadString(root, "warnings") ?? string.Empty).Trim();
            }
            catch (System.Text.Json.JsonException)
            {
                result.Raw = raw.Trim();
            }
            return result;
        }

        /// <summary>
        /// 剔除库中已存在（langKey + langCode）的译文，使不覆盖模式下只补空缺。
        /// </summary>
        private List<CommonLang> FilterExisting(List<CommonLang> list)
        {
            var keys = list.Select(x => x.LangKey).Distinct().ToList();
            var existed = _commonLangService.Queryable()
                .Where(x => keys.Contains(x.LangKey))
                .Select(x => new { x.LangKey, x.LangCode })
                .ToList()
                .Select(x => $"{x.LangKey}|{x.LangCode}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return list.Where(x => !existed.Contains($"{x.LangKey}|{x.LangCode}")).ToList();
        }

        private static string ReadString(JsonElement el, string name)
        {
            return el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }

        /// <summary>
        /// 规范化语言代码：去空格转小写，仅保留字母数字与 - _，并截断到列长。
        /// 非法输入返回 null 由调用方跳过，避免把脏数据写进 lang_code。
        /// </summary>
        private static string NormalizeLangCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var chars = code.Trim().ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
                .ToArray();
            if (chars.Length == 0) return null;

            return chars.Length <= LangCodeMaxLength
                ? new string(chars)
                : new string(chars, 0, LangCodeMaxLength);
        }

    }
}
