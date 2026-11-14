using Microsoft.AspNetCore.Mvc;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;

namespace ZR.Admin.WebApi.Controllers.AI
{
    /// <summary>
    /// AI 权限额度、模型价格与 Provider 诊断管理控制器。
    /// 提供 AI 能力查询、治理策略管理、模型价格维护、配置检查和健康诊断等功能。
    /// </summary>
    [Route("aiGovernance")]
    [ApiExplorerSettings(GroupName = "ai")]
    public class AiGovernanceController : BaseController
    {
        private readonly IAiGovernanceService _service;
        private readonly IAiConfigService _configService;
        private readonly IAiToolCallLogService _toolCallLogService;

        public AiGovernanceController(IAiGovernanceService service, IAiConfigService configService,
            IAiToolCallLogService toolCallLogService)
        {
            _service = service;
            _configService = configService;
            _toolCallLogService = toolCallLogService;
        }

        /// <summary>
        /// 获取 AI 能力列表。
        /// </summary>
        [HttpGet("capabilities")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult Capabilities() => SUCCESS(_service.GetCapabilities());

        /// <summary>
        /// 获取 AI 能力目录信息。
        /// </summary>
        [HttpGet("catalog")]
        [ActionPermissionFilter(Permission = "ai:governance:list,ai:price:list,ai:usage:list")]
        public IActionResult Catalog() => SUCCESS(_service.GetCatalog());

        /// <summary>
        /// 查询治理策略列表。
        /// </summary>
        [HttpGet("policy/list")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult PolicyList([FromQuery] AiPolicyQueryDto query) => SUCCESS(_service.GetPolicyList(query));

        /// <summary>
        /// 查询治理策略适用主体集合。
        /// <paramref name="keyword"/>
        /// <paramref name="scopeType"/>
        /// </summary>
        [HttpGet("policy/subjects")]
        [ActionPermissionFilter(Permission = "ai:governance:list,ai:governance:add,ai:governance:edit")]
        public IActionResult PolicySubjects(string scopeType, string keyword = null) => SUCCESS(_service.GetPolicySubjects(scopeType, keyword));

        /// <summary>
        /// 根据主键查询治理策略详情
        /// <paramref name="id"/>
        /// </summary>
        [HttpGet("policy/{id:long}")]
        [ActionPermissionFilter(Permission = "ai:governance:query")]
        public IActionResult Policy(long id) => SUCCESS(_service.GetPolicy(id));

        /// <summary>
        /// 新增治理策略。
        /// </summary>
        [HttpPost("policy")]
        [ActionPermissionFilter(Permission = "ai:governance:add")]
        [Log(Title = "AI治理策略", BusinessType = BusinessType.INSERT)]
        public IActionResult AddPolicy([FromBody] AiPolicySaveDto input)
        {
            input.Id = 0;
            return SUCCESS(new { id = _service.SavePolicy(input) });
        }

        /// <summary>
        /// 修改治理策略。
        /// </summary>
        [HttpPut("policy")]
        [ActionPermissionFilter(Permission = "ai:governance:edit")]
        [Log(Title = "AI治理策略", BusinessType = BusinessType.UPDATE)]
        public IActionResult EditPolicy([FromBody] AiPolicySaveDto input) => SUCCESS(_service.SavePolicy(input));

        /// <summary>
        /// 删除治理策略。
        /// </summary>
        [HttpDelete("policy/{id:long}")]
        [ActionPermissionFilter(Permission = "ai:governance:remove")]
        [Log(Title = "AI治理策略", BusinessType = BusinessType.DELETE)]
        public IActionResult DeletePolicy(long id) => SUCCESS(_service.DeletePolicy(id));

        /// <summary>
        /// 查询模型价格列表。
        /// </summary>
        [HttpGet("price/list")]
        [ActionPermissionFilter(Permission = "ai:price:list")]
        public IActionResult PriceList([FromQuery] AiModelPriceQueryDto query) => SUCCESS(_service.GetPriceList(query));

        /// <summary>
        /// 根据主键查询模型价格详情。
        /// </summary>
        [HttpGet("price/{id:long}")]
        [ActionPermissionFilter(Permission = "ai:price:query")]
        public IActionResult Price(long id) => SUCCESS(_service.GetPrice(id));

        /// <summary>
        /// 新增模型价格。
        /// </summary>
        [HttpPost("price")]
        [ActionPermissionFilter(Permission = "ai:price:add")]
        [Log(Title = "AI模型价格", BusinessType = BusinessType.INSERT)]
        public IActionResult AddPrice([FromBody] AiModelPriceSaveDto input)
        {
            input.Id = 0;
            return SUCCESS(new { id = _service.SavePrice(input) });
        }

        /// <summary>
        /// 修改模型价格。
        /// </summary>
        [HttpPut("price")]
        [ActionPermissionFilter(Permission = "ai:price:edit")]
        [Log(Title = "AI模型价格", BusinessType = BusinessType.UPDATE)]
        public IActionResult EditPrice([FromBody] AiModelPriceSaveDto input) => SUCCESS(_service.SavePrice(input));

        /// <summary>
        /// 删除模型价格。
        /// </summary>
        [HttpDelete("price/{id:long}")]
        [ActionPermissionFilter(Permission = "ai:price:remove")]
        [Log(Title = "AI模型价格", BusinessType = BusinessType.DELETE)]
        public IActionResult DeletePrice(long id) => SUCCESS(_service.DeletePrice(id));

        /// <summary>
        /// 检查 AI 配置项是否正确。
        /// </summary>
        [HttpGet("config/check")]
        [ActionPermissionFilter(Permission = "ai:governance:health")]
        public IActionResult ConfigCheck() => SUCCESS(_service.CheckConfiguration());

        /// <summary>
        /// 查看配置的实际生效来源：本次读的是 ai_config 还是 ai.json、渠道来自 ai_provider 还是文件，
        /// 并返回合并优先级与回退告警，用于排查“本地正常、线上异常”。
        /// </summary>
        [HttpGet("config/effective")]
        [ActionPermissionFilter(Permission = "ai:governance:health,ai:governance:list")]
        public IActionResult ConfigEffective() => SUCCESS(_service.GetConfigDiagnostics());

        /// <summary>
        /// 执行 AI 服务健康检查。
        /// </summary>
        [HttpPost("health")]
        [ActionPermissionFilter(Permission = "ai:governance:health")]
        public async Task<IActionResult> Health() => SUCCESS(await _service.CheckHealthAsync());

        /// <summary>
        /// 查看当前生效的 AI 运行时配置：库中值优先（密钥掩码），无则回显文件默认值。
        /// 同时返回配置源策略、当前环境、数据库覆盖是否生效、版本与修改人。
        /// </summary>
        [HttpGet("config/current")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult ConfigCurrent([FromQuery] string section = "AiOptions")
            => SUCCESS(_configService.GetCurrent(section));

        /// <summary>
        /// 保存 AI 运行时配置（整段 JSON）。校验格式后加密落库，版本 +1，本进程立即生效。
        /// 密钥回显为 ****xxxx 时按"未修改"处理，沿用库中已有值。
        /// </summary>
        [HttpPost("config")]
        [ActionPermissionFilter(Permission = "ai:governance:edit")]
        [Log(Title = "AI运行时配置", BusinessType = BusinessType.UPDATE)]
        public IActionResult SaveConfig([FromBody] AiConfigSaveDto input, [FromQuery] string section = "AiOptions")
            => SUCCESS(new { id = _configService.Save(input, section) });

        /// <summary>
        /// 删除库中 AI 配置，回退到 ai.json 默认值（本地/排障用；线上慎用）。
        /// </summary>
        [HttpDelete("config")]
        [ActionPermissionFilter(Permission = "ai:governance:remove")]
        [Log(Title = "AI运行时配置", BusinessType = BusinessType.DELETE)]
        public IActionResult ResetConfig([FromQuery] string section = "AiOptions")
            => SUCCESS(new { affected = _configService.Reset(section) });

        /// <summary>
        /// 手动清理本进程的 AI 配置缓存（直接改库、或多实例发布后使用）。
        /// 多实例部署下其它实例仍按 60 秒 TTL 自然过期。
        /// </summary>
        [HttpPost("config/refresh")]
        [ActionPermissionFilter(Permission = "ai:governance:edit")]
        public IActionResult RefreshConfig()
            => SUCCESS(new { refreshed = _configService.RefreshCache(), at = DateTime.Now });

        /// <summary>
        /// 查询 AI 工具调用明细（审计）：谁在什么时候调用了哪个工具、参数摘要、结果状态、耗时。
        /// 用于排查越权尝试（status=denied）与误调用（unknown_tool/failed/exception）。
        /// 数据范围由服务按登录身份收窄，不信任前端传入的租户/用户。
        /// </summary>
        [HttpGet("toolLog/list")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult ToolLogList([FromQuery] AiToolCallQueryDto query)
            => SUCCESS(_toolCallLogService.GetList(query, isAdmin: true));
    }
}
