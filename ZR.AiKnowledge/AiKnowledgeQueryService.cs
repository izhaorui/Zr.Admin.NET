using SqlSugar;
using ZR.AiKnowledge.Model;
using ZR.Model;
using ZR.Model.AI;
using ZR.ServiceCore;

namespace ZR.AiKnowledge
{
    /// <summary>知识库检索审计的查询与统计（管理端排障用）。</summary>
    public interface IAiKnowledgeQueryService
    {
        /// <summary>分页查询审计流水，按时间倒序。</summary>
        PagedInfo<AiKnowledgeQueryLogDto> GetList(AiKnowledgeQueryDto query);

        /// <summary>近 N 天概览：命中率、耗时、按天趋势、Top 未命中问题。</summary>
        AiKnowledgeQueryStatDto GetStat(int days);
    }

    /// <summary>
    /// 检索审计的读取侧实现。
    /// <para>
    /// 统计窗口最大 90 天，且只拉取聚合所需列（时间/状态/耗时/问题），不读 HitTitles 等大字段；
    /// 若后续窗口或数据量继续增大，应改为按天预聚合，而不是把窗口拉长到全表。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IAiKnowledgeQueryService), ServiceLifetime = LifeTime.Transient)]
    public class AiKnowledgeQueryService : BaseService<AiKnowledgeQueryLog>, IAiKnowledgeQueryService
    {
        public PagedInfo<AiKnowledgeQueryLogDto> GetList(AiKnowledgeQueryDto query)
        {
            query ??= new AiKnowledgeQueryDto();

            var expression = Expressionable.Create<AiKnowledgeQueryLog>();
            expression.AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime.Value);
            expression.AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime.Value);
            expression.AndIF(!string.IsNullOrWhiteSpace(query.Keyword), x => x.Question.Contains(query.Keyword.Trim()));
            expression.AndIF(!string.IsNullOrWhiteSpace(query.Status), x => x.Status == query.Status.Trim());
            expression.AndIF(!string.IsNullOrWhiteSpace(query.Provider), x => x.Provider == query.Provider.Trim());
            expression.AndIF(!string.IsNullOrWhiteSpace(query.KbId), x => x.KbId == query.KbId.Trim());
            expression.AndIF(query.UserId.HasValue && query.UserId.Value > 0, x => x.UserId == query.UserId.Value);

            var pageNum = Math.Max(1, query.PageNum);
            var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 200);
            var total = 0;
            var rows = Queryable()
                .Where(expression.ToExpression())
                .OrderBy(x => x.CreateTime, OrderByType.Desc)
                .OrderBy(x => x.Id, OrderByType.Desc)
                .ToPageList(pageNum, pageSize, ref total);

            return new PagedInfo<AiKnowledgeQueryLogDto>
            {
                PageIndex = pageNum,
                PageSize = pageSize,
                TotalNum = total,
                Result = rows.Select(ToDto).ToList()
            };
        }

        public AiKnowledgeQueryStatDto GetStat(int days)
        {
            days = Math.Clamp(days <= 0 ? 7 : days, 1, 90);
            var begin = DateTime.Today.AddDays(-(days - 1));

            var rows = Context.Queryable<AiKnowledgeQueryLog>()
                .Where(x => x.CreateTime >= begin)
                .Select(x => new AiKnowledgeStatRow
                {
                    CreateTime = x.CreateTime,
                    Status = x.Status,
                    DurationMs = x.DurationMs,
                    Question = x.Question,
                    KbId = x.KbId,
                    KbName = x.KbName
                })
                .ToList();

            var total = rows.Count;
            var hit = rows.Count(r => IsStatus(r, "success"));
            var empty = rows.Count(r => IsStatus(r, "empty"));
            var failed = rows.Count(r => IsStatus(r, "failed"));

            var stat = new AiKnowledgeQueryStatDto
            {
                Days = days,
                Total = total,
                Hit = hit,
                Empty = empty,
                Failed = failed,
                HitRate = total > 0 ? Math.Round((double)hit / total, 4) : 0,
                AvgDurationMs = total > 0 ? Math.Round(rows.Average(r => (double)r.DurationMs), 1) : 0,
                MaxDurationMs = total > 0 ? rows.Max(r => r.DurationMs) : 0
            };

            // 先按日期分组再逐天取，避免 O(天数 × 记录数) 的重复扫描
            var byDate = rows
                .Where(r => r.CreateTime.HasValue)
                .GroupBy(r => r.CreateTime.Value.Date)
                .ToDictionary(g => g.Key, g => g.ToList());
            for (var day = begin; day <= DateTime.Today; day = day.AddDays(1))
            {
                byDate.TryGetValue(day, out var dayRows);
                dayRows ??= new List<AiKnowledgeStatRow>();
                stat.ByDay.Add(new AiKnowledgeDailyStatDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Total = dayRows.Count,
                    Hit = dayRows.Count(r => IsStatus(r, "success")),
                    Empty = dayRows.Count(r => IsStatus(r, "empty")),
                    Failed = dayRows.Count(r => IsStatus(r, "failed"))
                });
            }

            stat.TopMissed = rows
                .Where(r => IsStatus(r, "empty") && !string.IsNullOrWhiteSpace(r.Question))
                .GroupBy(r => r.Question.Trim())
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new AiKnowledgeMissedQuestionDto { Question = g.Key, Count = g.Count() })
                .ToList();

            // 按库拆分：命中率高低要能落到具体库，否则多库下"总命中率 70%"掩盖了"某个库全不命中"
            stat.ByBase = rows
                .GroupBy(r => r.KbId ?? string.Empty)
                .Select(g =>
                {
                    var baseTotal = g.Count();
                    var baseHit = g.Count(r => IsStatus(r, "success"));
                    return new AiKnowledgeBaseStatDto
                    {
                        KbId = g.Key.Length == 0 ? null : g.Key,
                        KbName = g.Select(r => r.KbName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                        Total = baseTotal,
                        Hit = baseHit,
                        Empty = g.Count(r => IsStatus(r, "empty")),
                        Failed = g.Count(r => IsStatus(r, "failed")),
                        HitRate = baseTotal > 0 ? Math.Round((double)baseHit / baseTotal, 4) : 0,
                        AvgDurationMs = Math.Round(g.Average(r => (double)r.DurationMs), 1)
                    };
                })
                .OrderByDescending(b => b.Total)
                .ToList();

            return stat;
        }

        private static bool IsStatus(AiKnowledgeStatRow row, string status)
            => string.Equals(row?.Status, status, StringComparison.OrdinalIgnoreCase);

        private static AiKnowledgeQueryLogDto ToDto(AiKnowledgeQueryLog entity) => new()
        {
            Id = entity.Id,
            CreateTime = entity.CreateTime,
            Question = entity.Question,
            Provider = entity.Provider,
            KbName = entity.KbName,
            KbId = entity.KbId,
            Status = entity.Status,
            HitCount = entity.HitCount,
            TopK = entity.TopK,
            TopScore = entity.TopScore.HasValue ? (double)entity.TopScore.Value : null,
            DurationMs = entity.DurationMs,
            ErrorCode = entity.ErrorCode,
            ErrorMsg = entity.ErrorMsg,
            HitTitles = entity.HitTitles,
            UserId = entity.UserId,
            UserName = entity.UserName,
            TraceId = entity.TraceId
        };

        /// <summary>统计用的精简投影：不带 HitTitles / ErrorMsg 等大字段。</summary>
        public class AiKnowledgeStatRow
        {
            public DateTime? CreateTime { get; set; }

            public string Status { get; set; }

            public long DurationMs { get; set; }

            public string Question { get; set; }

            public string KbId { get; set; }

            public string KbName { get; set; }
        }
    }
}