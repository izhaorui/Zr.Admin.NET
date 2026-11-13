using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.AI.Knowledge
{
    /// <summary>
    /// 一次知识库检索的审计快照，由 <c>search_knowledge</c> 工具在检索结束时填写。
    /// <para>
    /// 只放工具层能确定的事实（问题、渠道、命中数、耗时、成败）；
    /// 用户身份、TraceId、租户等运行环境信息由落库实现自行补齐，
    /// 避免插件工程为了审计去依赖 HttpContext。
    /// </para>
    /// </summary>
    public sealed class KnowledgeQueryAudit
    {
        /// <summary>检索语句（落库实现负责截断）</summary>
        public string Question { get; set; }

        public string KbId { get; set; }

        public string KbName { get; set; }

        /// <summary>检索渠道名</summary>
        public string Provider { get; set; }

        public int TopK { get; set; }

        public int HitCount { get; set; }

        public double TopScore { get; set; }

        public long DurationMs { get; set; }

        /// <summary>success / empty / failed</summary>
        public string Status { get; set; }

        /// <summary>错误分类（仅 Status=failed 时有值）</summary>
        public string ErrorCode { get; set; }

        /// <summary>错误摘要（不含地址与密钥）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>命中标题摘要</summary>
        public string HitTitles { get; set; }

        /// <summary>触发用户ID（工具执行上下文传入）</summary>
        public long UserId { get; set; }
    }

    /// <summary>
    /// 知识库检索审计落库契约。
    /// <para>
    /// 实现必须"尽力而为"：写入失败只能记日志，<b>不得</b>把异常抛回检索链路——
    /// 审计是旁路能力，不能因为它写不进去就让用户拿不到检索结果。
    /// </para>
    /// <para>
    /// 插件工程通过构造函数注入本接口；未注册实现时按"不审计"降级，
    /// 宿主删掉审计表或不需要审计时不必改插件代码。
    /// </para>
    /// </summary>
    public interface IKnowledgeQueryAuditor
    {
        Task RecordAsync(KnowledgeQueryAudit record, CancellationToken cancellationToken = default);
    }
}