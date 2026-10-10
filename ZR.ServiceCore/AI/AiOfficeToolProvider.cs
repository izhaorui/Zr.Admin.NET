using Infrastructure.AI;
using Infrastructure.Attribute;
using Newtonsoft.Json.Linq;
using ZR.Model.AI.Dto;
using ZR.Model.System.Dto;
using ZR.ServiceCore.AI.IService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.AI
{
    /// <summary>
    /// AI 助手内置办工工具：日程查询 / 日程解析 / 周报生成。
    /// 与各业务模块扩展工具一样通过 <see cref="IAiAssistantToolProvider"/> 注册（不再由 SysAiChatService 硬编码），
    /// 工具内部复用既有 Service，只访问“当前登录用户自己的数据”（按 userId 隔离），因此不声明 Permission。
    /// </summary>
    [AppService(ServiceType = typeof(IAiAssistantToolProvider), ServiceLifetime = LifeTime.Transient)]
    public class AiOfficeToolProvider : IAiAssistantToolProvider
    {
        private const string ToolQueryMySchedules = "query_my_schedules";
        private const string ToolParseSchedule = "parse_schedule";
        private const string ToolGenerateWeeklyReport = "generate_weekly_report";

        private readonly ISysAiService _sysAi;
        private readonly IDailyScheduleService _scheduleService;

        public string ProviderName => "office";

        public AiOfficeToolProvider(ISysAiService sysAi, IDailyScheduleService scheduleService)
        {
            _sysAi = sysAi;
            _scheduleService = scheduleService;
        }

        public List<AiToolDef> GetToolDefs()
        {
            return new List<AiToolDef>
            {
                new AiToolDef
                {
                    Name = ToolQueryMySchedules,
                    Label = "日程查询",
                    Samples = new List<string>
                    {
                        "我今天有什么日程安排？",
                        "帮我看看最近一周的日程"
                    },
                    Description = "查询当前登录用户指定日期范围内的日程安排（按截止/完成/创建时间任一命中，含已完成）。不传参数默认查最近 7 天。用于“我今天有什么安排/本周日程/某天日程”。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["startDate"] = new { type = "string", description = "开始日期，格式 yyyy-MM-dd，可省略" },
                            ["endDate"] = new { type = "string", description = "结束日期（含当天），格式 yyyy-MM-dd，可省略" }
                        },
                        required = Array.Empty<string>()
                    }
                },
                new AiToolDef
                {
                    Name = ToolParseSchedule,
                    Label = "日程解析",
                    Samples = new List<string>
                    {
                        "帮我记一下：明天上午10点和产品过需求"
                    },
                    Description = "把用户口语化的日程描述解析成结构化日程草稿（标题/内容/优先级/截止/提醒）。只生成草稿不落库。用于“帮我记一个日程：...”等表达。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["text"] = new { type = "string", description = "用户原始口语描述" }
                        },
                        required = new[] { "text" }
                    }
                },
                new AiToolDef
                {
                    Name = ToolGenerateWeeklyReport,
                    Label = "周报生成",
                    Samples = new List<string>
                    {
                        "帮我写一下本周的工作周报"
                    },
                    Description = "汇总当前用户本周日程自动生成工作周报草稿（Markdown）。用于“帮我写周报/本周总结”。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>(),
                        required = Array.Empty<string>()
                    }
                }
            };
        }

        public async Task<AiToolExecResult> ExecuteAsync(string toolName, string argsJson, long userId)
        {
            switch (toolName)
            {
                case ToolQueryMySchedules:
                    return await QueryMySchedulesAsync(ParseArgs(argsJson), userId);
                case ToolParseSchedule:
                    return await ParseScheduleAsync(ParseArgs(argsJson));
                case ToolGenerateWeeklyReport:
                    return await GenerateWeeklyReportAsync(userId);
                default:
                    return null;
            }
        }

        /// <summary>解析工具参数：空/空白按空对象处理（与 SysAiChatService 原行为一致）。</summary>
        private static JObject ParseArgs(string argsJson)
            => string.IsNullOrWhiteSpace(argsJson) ? new JObject() : JObject.Parse(argsJson);

        /// <summary>查询本人日程</summary>
        private async Task<AiToolExecResult> QueryMySchedulesAsync(JObject args, long userId)
        {
            var startDate = AiHelper.TryParseDate(args["startDate"]?.Value<string>()) ?? DateTime.Today;
            var endDate = AiHelper.TryParseDate(args["endDate"]?.Value<string>()) ?? startDate.AddDays(6);
            endDate = endDate.Date.AddDays(1).AddSeconds(-1);

            var list = await _scheduleService.GetByDateRangeAsync(userId, startDate, endDate);
            if (list == null || list.Count == 0)
            {
                return AiToolExecResult.Success(
                    $"在 {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd} 期间未查询到日程安排。");
            }
            var lines = list.Take(50).Select(s =>
            {
                var status = s.Status == "1" ? "已完成" : "未完成";
                var pri = s.Priority switch { 1 => "低", 3 => "高", _ => "中" };
                var due = s.DueTime?.ToString("MM-dd HH:mm") ?? "无截止";
                var content = string.IsNullOrWhiteSpace(s.Content) ? "" : $" | {AiHelper.ClipText(s.Content, 120)}";
                return $"- [{status}][优先级{pri}] {s.Title}（截止 {due}）{content}";
            });
            var tail = list.Count > 50 ? $"\n（共 {list.Count} 条，仅显示前 50 条）" : "";
            return AiToolExecResult.Success(
                $"查询到 {list.Count} 条日程：\n" + string.Join("\n", lines) + tail);
        }

        /// <summary>日程口语解析（草稿，不落库）</summary>
        private async Task<AiToolExecResult> ParseScheduleAsync(JObject args)
        {
            var text = args["text"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return AiToolExecResult.Error("缺少待解析的日程描述 text");
            }
            var result = await _sysAi.ParseScheduleAsync(new SysAiScheduleParseInput { Text = text });
            var pri = result.Priority switch { 1 => "低", 3 => "高", _ => "中" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"日程草稿解析完成（未保存，展示给用户确认即可）：");
            sb.AppendLine($"- 标题：{result.Title}");
            if (!string.IsNullOrWhiteSpace(result.Content)) sb.AppendLine($"- 内容：{result.Content}");
            sb.AppendLine($"- 优先级：{result.Priority}({pri})");
            if (!string.IsNullOrWhiteSpace(result.DueTime)) sb.AppendLine($"- 截止时间：{result.DueTime}");
            if (!string.IsNullOrWhiteSpace(result.ReminderTime)) sb.AppendLine($"- 提醒时间：{result.ReminderTime}");
            if (!string.IsNullOrWhiteSpace(result.Warnings)) sb.AppendLine($"- 说明：{result.Warnings}");
            return AiToolExecResult.Success(sb.ToString());
        }

        /// <summary>本周周报草稿（Markdown，不落库）</summary>
        private async Task<AiToolExecResult> GenerateWeeklyReportAsync(long userId)
        {
            var result = await _sysAi.GenerateWeeklyReportAsync(new SysAiWeeklyReportInput(), userId);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"本周周报已生成（统计周期 {result.PeriodStart} ~ {result.PeriodEnd}，共 {result.Total} 条日程）：");
            if (!string.IsNullOrWhiteSpace(result.Summary)) sb.AppendLine($"\n【整体情况】\n{result.Summary}");
            if (result.Completed.Count > 0) sb.AppendLine($"\n【已完成】\n" + string.Join("\n", result.Completed.Select(x => $"- {x}")));
            if (result.Pending.Count > 0) sb.AppendLine($"\n【未完成/待办】\n" + string.Join("\n", result.Pending.Select(x => $"- {x}")));
            if (result.Risks.Count > 0) sb.AppendLine($"\n【风险提示】\n" + string.Join("\n", result.Risks.Select(x => $"- {x}")));
            return AiToolExecResult.Success(sb.ToString());
        }
    }
}
