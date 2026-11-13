using System.Threading;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.AI.Knowledge;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using ZR.Model.AI;

namespace ZR.ServiceCore.AI.Knowledge
{
    /// <summary>
    /// 知识库检索审计落库实现。
    /// <para>
    /// 写入一律吞异常并记 Warn：审计属于旁路，数据库抖动、表未建、字段超长都不应让
    /// 用户拿不到检索结果。表结构由 <c>DbMigrationService</c> 的主库迁移清单维护。
    /// </para>
    /// <para>
    /// 运行环境信息（用户、TraceId、租户）在这里补齐，插件只负责传业务事实。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IKnowledgeQueryAuditor), ServiceLifetime = LifeTime.Transient)]
    public class KnowledgeQueryAuditor : BaseService<AiKnowledgeQueryLog>, IKnowledgeQueryAuditor
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public async Task RecordAsync(KnowledgeQueryAudit record, CancellationToken cancellationToken = default)
        {
            if (record == null) return;

            try
            {
                var entity = new AiKnowledgeQueryLog
                {
                    Question = Clip(record.Question, 500),
                    KbId = Clip(record.KbId, 64),
                    KbName = Clip(record.KbName, 100),
                    Provider = Clip(record.Provider, 32),
                    TopK = record.TopK,
                    HitCount = record.HitCount,
                    TopScore = record.TopScore > 0 ? (decimal?)Math.Round((decimal)record.TopScore, 4) : null,
                    DurationMs = record.DurationMs < 0 ? 0 : record.DurationMs,
                    Status = Clip(record.Status, 16) ?? "failed",
                    ErrorCode = Clip(record.ErrorCode, 32),
                    ErrorMsg = Clip(record.ErrorMessage, 1000),
                    HitTitles = Clip(record.HitTitles, 1000),
                    UserId = record.UserId,
                    UserName = Clip(App.HttpContext?.GetName(), 64),
                    TenantId = Clip(App.GetCurrentTenantId() ?? App.MainDbConfigId, 64),
                    TraceId = Clip(App.HttpContext?.TraceIdentifier, 64)
                };

                // 雪花ID：主键非自增，不显式生成会插成 0 并在第二次写入时撞主键
                await Context.Insertable(entity).ExecuteReturnSnowflakeIdAsync();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"知识库检索审计写入失败 user={record.UserId} status={record.Status} q={Clip(record.Question, 80)}");
            }
        }

        /// <summary>按列宽截断（中文按字符计，不做字节处理）。</summary>
        private static string Clip(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var value = text.Trim();
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}