using ZR.Model.System;

namespace ZR.Model.AI
{
    /// <summary>
    /// AI 模型渠道（平台级，固定系统库）。
    /// <para>
    /// 一行一个渠道 = 一个可独立启停/排序/测试的接入点；同一厂商可有多行（生产 Key / 备用 Key），
    /// 由 <see cref="Priority"/>、<see cref="Sort"/>、<see cref="Weight"/> 决定同一 Provider 下的选中顺序。
    /// </para>
    /// <para>
    /// 设计依据：参考 New API 的渠道模型（优先级/权重/参数覆盖）与 RuoYi-Vue-Plus 的 provider 独立成表；
    /// 模型候选、参数覆盖、模型映射用 JSON 列承载，避免为每个新参数加列。
    /// </para>
    /// <para>
    /// 路由说明与 AiModelPrice 一致：[Tenant("0")] + IMainDbEntity 同时存在，多租户开/关都能定位到系统库。
    /// </para>
    /// </summary>
    [SugarTable("ai_provider", "AI模型渠道")]
    [Tenant("0")]
    [SugarIndex("uk_ai_provider_env_name", nameof(Environment), OrderByType.Asc, true)]
    [SugarIndex("uk_ai_provider_env_name", nameof(Name), OrderByType.Asc, true)]
    [SugarIndex("idx_ai_provider_pick", nameof(Environment), OrderByType.Asc)]
    [SugarIndex("idx_ai_provider_pick", nameof(Status), OrderByType.Asc)]
    [SugarIndex("idx_ai_provider_pick", nameof(Priority), OrderByType.Asc)]
    public class AiProvider : SysBase, IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>环境名（Development / Production）；* 表示所有环境通用。</summary>
        [SugarColumn(Length = 20, DefaultValue = "*")]
        public string Environment { get; set; } = "*";

        /// <summary>厂商标识（qwen / openai / deepseek …），与 ai_model_price.Provider 同一口径。</summary>
        [SugarColumn(Length = 32)]
        public string Provider { get; set; }

        /// <summary>渠道名（同厂商可多行），同环境内唯一，用于人工识别与排障。</summary>
        [SugarColumn(Length = 64)]
        public string Name { get; set; }

        /// <summary>接口基地址；留空回退厂商默认。</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string BaseUrl { get; set; }

        /// <summary>聊天接口路径；留空回退默认 /chat/completions。</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string ChatEndpoint { get; set; }

        /// <summary>访问密钥（DataProtection 密文，enc:v1: 前缀）。列名显式标注 Cipher，避免被当明文读取。</summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string ApiKeyCipher { get; set; }

        /// <summary>默认模型名（与 ai_model_price.Model 长度一致）。</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string Model { get; set; }

        /// <summary>看图模型；留空表示该渠道不提供多模态。</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string VisionModel { get; set; }

        /// <summary>候选模型列表（JSON 数组，仅用于下拉展示，不参与查询）。</summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string ModelsJson { get; set; }

        /// <summary>请求参数覆盖（JSON 对象，预留；如 {"top_p":0.9}）。</summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string OverrideJson { get; set; }

        /// <summary>模型名映射（JSON 对象，预留；逻辑名 → 上游模型名）。</summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string ModelMappingJson { get; set; }

        /// <summary>渠道级超时（秒）；为空回退全局配置。</summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>优先级（越大越优先），同 Provider 多渠道时用于选中。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Priority { get; set; }

        /// <summary>同优先级下的权重（预留，后续用于轮询；当前不参与选中）。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Weight { get; set; }

        /// <summary>连续失败自动禁用（预留开关，0 未开启）。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int AutoDisable { get; set; }

        /// <summary>0 启用；1 停用（停用不参与选中，但保留历史与审计）。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        /// <summary>同优先级下的展示/选择顺序（升序）。</summary>
        [SugarColumn(DefaultValue = "0")]
        public int Sort { get; set; }

        /// <summary>每次保存递增，便于缓存失效与“当前生效版本”排查。</summary>
        [SugarColumn(DefaultValue = "0")]
        public long Version { get; set; }
    }
}
