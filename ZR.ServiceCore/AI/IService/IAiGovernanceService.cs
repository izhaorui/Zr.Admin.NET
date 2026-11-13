using ZR.Model;
using ZR.Model.AI;
using ZR.Model.AI.Dto;

namespace ZR.ServiceCore.AI.IService
{
    /// <summary>
    /// AI 治理管理服务。
    /// 约定：菜单/接口级权限码校验由 AiGovernanceController 的 ActionPermissionFilter 负责，本服务不重复校验，
    /// 只做过滤器表达不了的判断——策略的租户归属（AuthorizePolicy）与请求体决定的操作类型（SavePolicy）。
    /// 非 HTTP 调用方（后台任务、其他模块）须自行鉴权后再调用。
    /// </summary>
    public interface IAiGovernanceService
    {
        PagedInfo<AiPolicyListDto> GetPolicyList(AiPolicyQueryDto query);
        AiAccessPolicy GetPolicy(long id);
        List<AiPolicySubjectDto> GetPolicySubjects(string scopeType, string keyword);
        long SavePolicy(AiPolicySaveDto input);
        int DeletePolicy(long id);

        PagedInfo<AiModelPrice> GetPriceList(AiModelPriceQueryDto query);
        AiModelPrice GetPrice(long id);
        long SavePrice(AiModelPriceSaveDto input);
        int DeletePrice(long id);

        AiGovernanceCapabilitiesDto GetCapabilities();
        AiModelCatalogDto GetCatalog();
        AiConfigCheckDto CheckConfiguration();

        /// <summary>配置实际生效来源（ai_config / ai.json / ai_provider）与合并优先级诊断。</summary>
        AiConfigDiagnosticsDto GetConfigDiagnostics();

        Task<AiHealthCheckDto> CheckHealthAsync();
    }
}
