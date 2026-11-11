using ZR.Model.AI.Dto;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// AI 运行时配置管理（数据库覆盖层）：查看、保存、回退。
    /// <para>
    /// 保存后立即使本进程缓存失效；多实例部署下其它实例最多 60 秒后生效（读取侧带短 TTL 缓存）。
    /// 密钥以 DataProtection 密文落库，查看接口一律返回掩码。
    /// </para>
    /// </summary>
    public interface IAiConfigService
    {
        /// <summary>当前配置状态（库中值优先，否则文件默认值；密钥均掩码）。</summary>
        AiConfigCurrentDto GetCurrent(string section = "AiOptions");

        /// <summary>保存/更新指定段的配置，返回记录 Id。JSON 非法或表不存在时抛 CustomException。</summary>
        long Save(AiConfigSaveDto input, string section = "AiOptions");

        /// <summary>删除库中该段配置（回退到文件配置），返回影响行数。</summary>
        int Reset(string section = "AiOptions");
    }
}
