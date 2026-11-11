using System.ComponentModel.DataAnnotations;

namespace ZR.Model.AI.Dto
{
    /// <summary>渠道列表查询条件。</summary>
    public class AiProviderQueryDto
    {
        /// <summary>按渠道名/厂商标识模糊匹配</summary>
        public string Keyword { get; set; }

        /// <summary>厂商标识精确匹配</summary>
        public string Provider { get; set; }

        /// <summary>0 启用 / 1 停用；空=全部</summary>
        public int? Status { get; set; }

        public int PageNum { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }

    /// <summary>渠道列表/详情（密钥只回显掩码，不返回密文与明文）。</summary>
    public class AiProviderListDto
    {
        public long Id { get; set; }

        public string Environment { get; set; }

        public string Provider { get; set; }

        public string Name { get; set; }

        public string BaseUrl { get; set; }

        public string ChatEndpoint { get; set; }

        public string Model { get; set; }

        public string VisionModel { get; set; }

        /// <summary>候选模型（由 ModelsJson 解析，供下拉使用）</summary>
        public List<string> Models { get; set; } = new();

        public int? TimeoutSeconds { get; set; }

        public int Priority { get; set; }

        public int Weight { get; set; }

        public int AutoDisable { get; set; }

        /// <summary>0 启用 / 1 停用</summary>
        public int Status { get; set; }

        public int Sort { get; set; }

        public long Version { get; set; }

        /// <summary>密钥掩码（****后四位）；未配置时为空</summary>
        public string ApiKeyMask { get; set; }

        public string Remark { get; set; }

        public string Update_by { get; set; }

        public DateTime? Update_time { get; set; }
    }

    /// <summary>新增/修改渠道。ApiKey 传掩码或留空表示不修改。</summary>
    public class AiProviderSaveDto
    {
        /// <summary>0=新增，&gt;0=修改</summary>
        public long Id { get; set; }

        [Required(ErrorMessage = "厂商标识不能为空")]
        [StringLength(32, ErrorMessage = "厂商标识最多 32 个字符")]
        public string Provider { get; set; }

        [Required(ErrorMessage = "渠道名不能为空")]
        [StringLength(64, ErrorMessage = "渠道名最多 64 个字符")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "BaseUrl 最多 500 个字符")]
        public string BaseUrl { get; set; }

        [StringLength(200, ErrorMessage = "ChatEndpoint 最多 200 个字符")]
        public string ChatEndpoint { get; set; }

        /// <summary>访问密钥：新值则加密保存；掩码（****）或留空则保留原密钥</summary>
        public string ApiKey { get; set; }

        [StringLength(100, ErrorMessage = "模型名最多 100 个字符")]
        public string Model { get; set; }

        [StringLength(100, ErrorMessage = "视觉模型最多 100 个字符")]
        public string VisionModel { get; set; }

        public List<string> Models { get; set; }

        [Range(1, 600, ErrorMessage = "超时需在 1-600 秒之间")]
        public int? TimeoutSeconds { get; set; }

        public int Priority { get; set; }

        public int Weight { get; set; }

        /// <summary>0 启用 / 1 停用</summary>
        public int Status { get; set; }

        public int Sort { get; set; }

        [StringLength(500, ErrorMessage = "备注最多 500 个字符")]
        public string Remark { get; set; }
    }

    /// <summary>渠道连通性测试入参：支持“未保存的表单值”直接测试。</summary>
    public class AiProviderTestInput
    {
        /// <summary>已保存渠道 Id；用于把掩码密钥替换为库中真实密钥</summary>
        public long Id { get; set; }

        public string Provider { get; set; }

        public string BaseUrl { get; set; }

        public string ChatEndpoint { get; set; }

        public string ApiKey { get; set; }

        public string Model { get; set; }

        public int? TimeoutSeconds { get; set; }
    }

    /// <summary>从上游拉取模型列表的结果。</summary>
    public class AiProviderModelsResult
    {
        public bool Ok { get; set; }

        public string Provider { get; set; }

        /// <summary>上游返回的模型标识（已去重排序），仅用于前端下拉回填，不写库</summary>
        public List<string> Models { get; set; } = new();

        /// <summary>成功为数量摘要，失败为可行动提示（不含密钥）</summary>
        public string Message { get; set; }
    }

    /// <summary>渠道连通性测试结果。</summary>
    public class AiProviderTestResult
    {
        public bool Ok { get; set; }

        public long LatencyMs { get; set; }

        public string Provider { get; set; }

        public string Model { get; set; }

        /// <summary>成功为模型回复摘要，失败为可行动提示（不含密钥与完整地址）</summary>
        public string Message { get; set; }
    }
}
