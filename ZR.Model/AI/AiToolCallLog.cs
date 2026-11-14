namespace ZR.Model.AI
{
    /// <summary>
    /// AI 工具调用审计流水（模型 function calling 每调用一次工具落一条）。
    /// <para>
    /// 与 ai_call_log（模型 HTTP 调用账务流水）互补：模型调用流水回答"花了多少 token"，
    /// 本表回答"模型替这个用户执行了什么工具、参数是什么、结果如何、耗时多少"，
    /// 用于排查两类问题：
    /// ① 越权：<see cref="Status"/> = denied，说明用户没有 <see cref="Permission"/> 声明的权限，
    ///    但仍被模型/提示词诱导发起了该工具调用（执行前被拦下，未真正查数据）；
    /// ② 误调用：<see cref="Status"/> = unknown_tool / failed / exception，说明工具名不存在、
    ///    参数不合 schema 或执行抛异常，可据 <see cref="ArgsSummary"/> 与 <see cref="ErrorMsg"/> 复盘。
    /// </para>
    /// <para>
    /// 只记"调用行为"本身：<see cref="ArgsSummary"/> 截断到 500 字并对密码/令牌类字段做掩码，
    /// 不记录工具返回正文，避免审计表变成第二份业务数据副本。
    /// <see cref="TraceId"/> 与同一请求的 ai_call_log.TraceId 同源，可把"模型调用 → 工具执行"串起来。
    /// </para>
    /// </summary>
    [SugarTable("ai_tool_call_log", "AI工具调用审计")]
    [Tenant(0)]
    [SugarIndex("idx_ai_tool_call_time", nameof(CreateTime), OrderByType.Desc)]
    [SugarIndex("idx_ai_tool_call_tenant", nameof(TenantId), OrderByType.Asc)]
    [SugarIndex("idx_ai_tool_call_tenant", nameof(CreateTime), OrderByType.Desc)]
    [SugarIndex("idx_ai_tool_call_tool", nameof(ToolName), OrderByType.Asc)]
    [SugarIndex("idx_ai_tool_call_status", nameof(Status), OrderByType.Asc)]
    [SugarIndex("idx_ai_tool_call_user", nameof(UserId), OrderByType.Asc)]
    public class AiToolCallLog : IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long Id { get; set; }

        /// <summary>能力标识：ai_chat（当前唯一来源，预留工作流等其他入口）</summary>
        [SugarColumn(Length = 64)]
        public string Scene { get; set; }

        /// <summary>会话ID（ai_chat_session.SessionId），便于按会话回放整轮工具轨迹</summary>
        public long SessionId { get; set; }

        /// <summary>工具名（模型 function calling 使用的 name，与工具目录一致）</summary>
        [SugarColumn(Length = 100)]
        public string ToolName { get; set; }

        /// <summary>归属提供者 ProviderName（office / chart / log-analysis / workflow / mall 等）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string Provider { get; set; }

        /// <summary>工具定义的权限码；为空表示该工具不做清单过滤（工具内部按 userId 隔离数据）</summary>
        [SugarColumn(Length = 128, IsNullable = true)]
        public string Permission { get; set; }

        /// <summary>参数摘要（已掩码敏感字段并截断 500 字，仅用于复盘调用意图）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string ArgsSummary { get; set; }

        /// <summary>success 成功 / failed 业务失败 / denied 无权限拦截 / unknown_tool 工具不存在 / exception 执行异常</summary>
        [SugarColumn(Length = 32)]
        public string Status { get; set; }

        /// <summary>1 成功、0 失败（含 denied），便于按结果做聚合统计</summary>
        public int Success { get; set; }

        /// <summary>面向排查的错误摘要（不含内部堆栈与密钥）</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string ErrorMsg { get; set; }

        /// <summary>本次工具执行耗时（毫秒，含权限校验）</summary>
        public long DurationMs { get; set; }

        /// <summary>与 ai_call_log.TraceId 同源</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TraceId { get; set; }

        /// <summary>触发用户ID（0 表示无登录上下文）</summary>
        public long UserId { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string UserName { get; set; }

        /// <summary>租户快照；单库部署固定系统库标识</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TenantId { get; set; }

        [SugarColumn(InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }
    }
}