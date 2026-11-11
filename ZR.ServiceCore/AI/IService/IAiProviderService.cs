using ZR.Model;
using ZR.Model.AI.Dto;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// AI 模型渠道管理：一行一个渠道，支持启停、优先级、连通性测试。
    /// <para>
    /// 渠道是运行期唯一真相（优先于 ai_config / ai.json 里的 Providers），
    /// 保存/删除后清空本进程缓存，多实例通过 60 秒 TTL 收敛。
    /// </para>
    /// </summary>
    public interface IAiProviderService
    {
        /// <summary>分页查询当前环境的渠道（密钥掩码）。</summary>
        PagedInfo<AiProviderListDto> GetList(AiProviderQueryDto query);

        /// <summary>渠道详情（密钥掩码）。</summary>
        AiProviderListDto Get(long id);

        /// <summary>新增/修改渠道，返回 Id。</summary>
        long Save(AiProviderSaveDto input);

        /// <summary>删除渠道（物理删除；调用流水不引用渠道 Id，无历史负担）。</summary>
        int Delete(long id);

        /// <summary>启用/停用。</summary>
        int ChangeStatus(long id, int status);

        /// <summary>用当前表单值（含未保存内容）做一次最小请求，验证地址/密钥/模型是否可用。</summary>
        Task<AiProviderTestResult> TestAsync(AiProviderTestInput input);

        /// <summary>
        /// 从上游模型列表接口拉取可用模型（仅返回给前端下拉回填，不写库）。
        /// 部分渠道（如 Azure OpenAI）不提供该接口，会返回可行动提示而不是异常。
        /// </summary>
        Task<AiProviderModelsResult> FetchModelsAsync(AiProviderTestInput input);
    }
}
