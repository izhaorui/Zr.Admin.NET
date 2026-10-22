namespace ZR.Model.AI.Dto
{
    /// <summary>
    /// AI 助手会话项（列表 / 新建返回）
    /// </summary>
    public class SysAiChatSessionDto
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        public long SessionId { get; set; }
        public string Title { get; set; }
        public string Model { get; set; }
        public DateTime? CreateTime { get; set; }
        public DateTime? UpdateTime { get; set; }
    }

    /// <summary>
    /// 历史消息项
    /// </summary>
    public class SysAiChatMessageDto
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        public long MessageId { get; set; }
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime? CreateTime { get; set; }
        /// <summary>本消息消耗输入 token（仅 assistant 消息有值；usage 缺失时为 null）</summary>
        public int? PromptTokens { get; set; }
        /// <summary>本消息输出 token</summary>
        public int? CompletionTokens { get; set; }
        /// <summary>本消息 token 合计</summary>
        public int? TotalTokens { get; set; }
        /// <summary>本条助手消息附带的图表（配置+后端数据），用户消息为空</summary>
        public List<AiChartViewDto> Charts { get; set; }

        /// <summary>本条用户消息附带的图片 URL（助手消息为空）</summary>
        public List<string> ImageUrls { get; set; }

        /// <summary>本条助手消息本轮调用过的工具（用户消息为空）</summary>
        public List<AiChatToolCallDto> Tools { get; set; }
    }

    /// <summary>
    /// 会话详情（标题 + 历史消息）
    /// </summary>
    public class SysAiChatDetailDto
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        public long SessionId { get; set; }
        public string Title { get; set; }
        public string Model { get; set; }
        public List<SysAiChatMessageDto> Messages { get; set; } = new();

        /// <summary>仅在指定 limit 分页时有效：是否还有更早的消息未返回</summary>
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// 发起对话请求。SessionId 为 0 时服务端自动新建会话
    /// </summary>
    public class SysAiChatRequestDto
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        public long SessionId { get; set; }
        [StringLength(2000, ErrorMessage = "消息长度不能超过 2000 字符")]
        public string Message { get; set; }

        /// <summary>
        /// 本轮图片（已上传后的访问 URL 或站点相对路径），最多 4 张。
        /// 有图时本轮走视觉模型、不调用工具；无图时走原文本+工具编排。
        /// </summary>
        [MaxLength(4, ErrorMessage = "一次最多发送 4 张图片")]
        public List<string> ImageUrls { get; set; }
    }

    /// <summary>
    /// 对话结果
    /// </summary>
    public class SysAiChatResultDto
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        public long SessionId { get; set; }
        public string Title { get; set; }
        public string Model { get; set; }
        /// <summary>助手最终回复（markdown）</summary>
        public string Reply { get; set; }
        /// <summary>本次是否新建了会话</summary>
        public bool IsNewSession { get; set; }
        /// <summary>本次对话累计输入 token（多次工具调用合并；模型未返回 usage 时为 null）</summary>
        public int? PromptTokens { get; set; }
        /// <summary>本次对话累计输出 token</summary>
        public int? CompletionTokens { get; set; }
        /// <summary>本次对话累计 token 合计</summary>
        public int? TotalTokens { get; set; }
        /// <summary>本轮助手回复附带的图表（配置+数据），无图时为空</summary>
        public List<AiChartViewDto> Charts { get; set; }

        /// <summary>本轮调用过的工具摘要</summary>
        public List<AiChatToolCallDto> Tools { get; set; }
    }

    /// <summary>
    /// 重命名会话
    /// </summary>
    public class SysAiChatRenameDto
    {
        [Required(ErrorMessage = "标题不能为空")]
        [StringLength(100)]
        public string Title { get; set; }
    }

    /// <summary>
    /// 供模型 function calling 识别的工具定义。
    /// Name/Description/Parameters 参与模型请求；Label/Permission 仅用于前端展示与清单过滤，不下发给模型。
    /// </summary>
    public class AiToolDef
    {
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>工具中文展示名（前端"正在调用 XX"）。留空时前端降级显示 Name。</summary>
        public string Label { get; set; }

        /// <summary>
        /// 可选权限码。非空时该工具仅对拥有此权限（或管理员）的用户出现在工具清单中，
        /// 避免把受限能力名暴露给无权限用户。留空表示不做清单过滤（工具内部仍按 userId 隔离数据）。
        /// </summary>
        public string Permission { get; set; }

        /// <summary>
        /// 示例问句（空态引导"猜你想问"，点击即填充/发送）。只随前端工具清单下发，不参与模型请求。
        /// 各 Provider 可按用户权限定制（重写 <c>GetToolDefsForUser</c>），如 chart 只给有权数据集的示例。
        /// </summary>
        public List<string> Samples { get; set; }

        /// <summary>JSON Schema（object），例如 { type="object", properties=..., required=... }</summary>
        public object Parameters { get; set; }
    }

    /// <summary>
    /// 下发给前端的工具清单项（GET aichat/tools）：工具展示名 + 空态引导示例问句。
    /// 不含 Description，避免把工具能力细节透给前端；Samples 只出现在当前用户有权使用的能力上。
    /// </summary>
    public class AiToolCatalogItemDto
    {
        /// <summary>工具名，与 SSE tool 事件的 ToolName 对应</summary>
        public string Name { get; set; }

        /// <summary>中文展示名；后端未登记时为 null，前端应降级显示 Name</summary>
        public string Label { get; set; }

        /// <summary>
        /// 示例问句（空态"猜你想问"，点击即填充/发送）。由后端按当前用户权限定制——
        /// 仅出现在用户有权使用的能力上（如图表只给有权数据集的示例）；为空时前端用兜底文案。
        /// </summary>
        public List<string> Samples { get; set; }
    }

    /// <summary>
    /// 全量 AI 工具目录项（管理端排查用）。与 <see cref="AiToolCatalogItemDto"/> 的区别：
    /// 不做用户权限过滤，且附带 Provider / 权限 / 说明，用于定位"某个工具由谁提供、
    /// 落在哪个模块、需要什么权限、能做什么"，解决工具增多后难以反查实现的问题。
    /// </summary>
    public class AiToolCatalogDetailDto
    {
        /// <summary>工具名（模型 function calling 使用的 name）</summary>
        public string Name { get; set; }

        /// <summary>中文展示名；未登记时为 null</summary>
        public string Label { get; set; }

        /// <summary>归属提供者 ProviderName（内置办工工具为 office，其余如 chart / log-analysis / workflow）</summary>
        public string Provider { get; set; }

        /// <summary>权限码；为空表示不做清单过滤（工具内部仍按用户隔离数据）</summary>
        public string Permission { get; set; }

        /// <summary>工具能力说明，供排查定位（仅管理端返回，普通用户清单不含该字段）</summary>
        public string Description { get; set; }

        /// <summary>默认示例问句（静态定义，未按用户权限定制）</summary>
        public List<string> Samples { get; set; }
    }

    /// <summary>
    /// 工具执行结果（回灌给模型的内容文本）
    /// </summary>
    public class AiToolExecResult
    {
        public bool Ok { get; set; }
        public string Content { get; set; }

        public AiChartQueryResult ChartQuery { get; set; }

        public static AiToolExecResult Success(string content) => new() { Ok = true, Content = content };
        public static AiToolExecResult Error(string content) => new() { Ok = false, Content = content };
    }

    /// <summary>
    /// AI 流式对话 SSE 事件。Type 决定本次事件读取哪些字段：
    /// <para>delta：文本增量（打字机逐字展示），读 Content；</para>
    /// <para>tool：工具执行状态，读 ToolName / ToolStatus(start|done) / ToolOk；</para>
    /// <para>done：整轮对话结束（已落库），读 SessionId/Title/Model/Reply/IsNewSession/tokens/Charts；</para>
    /// <para>error：异常终止，读 Error。</para>
    /// </summary>
    public class SysAiChatStreamDto
    {
        /// <summary>事件类型：delta | tool | done | error</summary>
        public string Type { get; set; }

        /// <summary>delta：本次增量文本</summary>
        public string Content { get; set; }

        /// <summary>tool：工具名</summary>
        public string ToolName { get; set; }

        /// <summary>tool：start=开始执行；done=执行结束</summary>
        public string ToolStatus { get; set; }

        /// <summary>tool(done)：工具是否执行成功</summary>
        public bool ToolOk { get; set; }

        /// <summary>done：会话 ID</summary>
        [JsonConverter(typeof(ValueToStringConverter))]
        public long SessionId { get; set; }

        /// <summary>done：会话标题</summary>
        public string Title { get; set; }

        /// <summary>done：实际使用模型</summary>
        public string Model { get; set; }

        /// <summary>done：助手最终回复（markdown，含图表占位由前端配合 Charts 渲染）</summary>
        public string Reply { get; set; }

        /// <summary>done：本次是否新建了会话</summary>
        public bool IsNewSession { get; set; }

        /// <summary>done：本次累计输入 token（无 usage 时为 null）</summary>
        public int? PromptTokens { get; set; }

        /// <summary>done：本次累计输出 token</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>done：本次累计 token 合计</summary>
        public int? TotalTokens { get; set; }

        /// <summary>done：本轮回复附带的图表（配置+数据），无图时为空</summary>
        public List<AiChartViewDto> Charts { get; set; }

        /// <summary>done：本轮工具调用摘要</summary>
        public List<AiChatToolCallDto> Tools { get; set; }

        /// <summary>error：错误描述（可展示给用户）</summary>
        public string Error { get; set; }
    }

    /// <summary>助手本轮一次工具调用的落库/展示摘要</summary>
    public class AiChatToolCallDto
    {
        public string Name { get; set; }
        public bool Ok { get; set; }
    }
}
