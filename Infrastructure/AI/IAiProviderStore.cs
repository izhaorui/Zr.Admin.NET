using System.Collections.Generic;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI 模型渠道的读取契约（平台级）。
    /// <para>
    /// 只返回“当前环境可用、已启用、已解密”的渠道，并按优先级排好序：
    /// 同一 Provider 出现多行时<b>全部返回</b>，首条为实际生效渠道，其余为备用（失败换 Key）。
    /// </para>
    /// <para>
    /// 返回空集合表示“库中没有渠道配置”，调用方回退到配置文件里的 Providers，
    /// 保证开源用户不建表也能启动。
    /// </para>
    /// </summary>
    public interface IAiProviderStore
    {
        /// <summary>当前环境的可用渠道（优先级降序、Sort 升序，同厂商多行按顺序全部保留）。无配置返回空集合。</summary>
        IReadOnlyList<AiProviderSnapshot> GetEnabledProviders();
    }

    /// <summary>
    /// 渠道投影：只带运行时需要的字段，避免 Infrastructure 反向依赖 ZR.Model。
    /// </summary>
    public sealed class AiProviderSnapshot
    {
        public long Id { get; set; }

        /// <summary>厂商标识（qwen/openai…）</summary>
        public string Provider { get; set; }

        /// <summary>渠道名（排障用，不参与请求）</summary>
        public string Name { get; set; }

        public string BaseUrl { get; set; }

        public string ChatEndpoint { get; set; }

        /// <summary>已解密的访问密钥</summary>
        public string ApiKey { get; set; }

        public string Model { get; set; }

        public string VisionModel { get; set; }

        public List<string> Models { get; set; } = new();

        public int? TimeoutSeconds { get; set; }

        public int Priority { get; set; }

        public int Weight { get; set; }
    }
}
