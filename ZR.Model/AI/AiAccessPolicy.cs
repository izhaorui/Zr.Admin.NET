using ZR.Model.System;

namespace ZR.Model.AI
{
    /// <summary>
    /// AI 访问与额度策略。平台治理统一配置，固定存放于系统库（ConfigId=0），跨租户共享。
    /// ScopeType 支持 global/tenant/role/user；TenantId、SubjectId 使用空值等价的规范值（""、0），便于建立唯一约束。
    /// <para>
    /// 路由说明：多租户关闭时靠 [Tenant("0")] 固定系统库；多租户开启时由 IMainDbEntity 走主库。
    /// 二者须同时存在，缺 [Tenant] 会导致多租户关闭时被路由到 MainDb 而找不到表。
    /// </para>
    /// </summary>
    [SugarTable("ai_access_policy", "AI访问与额度策略")]
    [Tenant("0")]
    [SugarIndex("uk_ai_policy_scope", nameof(ScopeType), OrderByType.Asc, true)]
    [SugarIndex("uk_ai_policy_scope", nameof(TenantId), OrderByType.Asc, true)]
    [SugarIndex("uk_ai_policy_scope", nameof(SubjectId), OrderByType.Asc, true)]
    [SugarIndex("uk_ai_policy_scope", nameof(Scene), OrderByType.Asc, true)]
    public class AiAccessPolicy : SysBase, IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>global / tenant / role / user</summary>
        [SugarColumn(Length = 16)]
        public string ScopeType { get; set; }

        /// <summary>租户标识；全局策略为空字符串。</summary>
        [SugarColumn(Length = 64, DefaultValue = "")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>角色或用户 ID；全局和租户策略为 0。</summary>
        [SugarColumn(DefaultValue = "0")]
        public long SubjectId { get; set; }

        /// <summary>能力场景；* 表示该作用域默认规则。</summary>
        [SugarColumn(Length = 64, DefaultValue = "*")]
        public string Scene { get; set; } = "*";

        /// <summary>空表示继承；0 禁用；1 启用。</summary>
        [SugarColumn(IsNullable = true)]
        public int? IsEnabled { get; set; }

        [SugarColumn(IsNullable = true)]
        public long? DailyTokenLimit { get; set; }

        [SugarColumn(IsNullable = true)]
        public long? MonthlyTokenLimit { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "decimal(18,6)")]
        public decimal? DailyAmountLimit { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "decimal(18,6)")]
        public decimal? MonthlyAmountLimit { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? ConcurrentLimit { get; set; }

        /// <summary>0 正常，1 停用。停用策略不参与计算。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }
    }
}
