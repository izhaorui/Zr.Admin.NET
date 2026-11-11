using Microsoft.AspNetCore.Mvc;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;

namespace ZR.Admin.WebApi.Controllers.AI
{
    /// <summary>
    /// AI 模型渠道管理（平台级）：一行一个渠道，支持启停、优先级与连通性测试。
    /// <para>
    /// 权限复用 AI 治理的 list/query/add/edit/remove/health，不新增权限码；
    /// 渠道是运行期唯一真相，保存后清缓存，多实例最多 60 秒收敛。
    /// </para>
    /// </summary>
    [Route("aiProvider")]
    [ApiExplorerSettings(GroupName = "ai")]
    public class AiProviderController : BaseController
    {
        private readonly IAiProviderService _service;

        public AiProviderController(IAiProviderService service)
        {
            _service = service;
        }

        /// <summary>分页查询渠道（密钥只回显掩码）。</summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "ai:governance:list")]
        public IActionResult List([FromQuery] AiProviderQueryDto query) => SUCCESS(_service.GetList(query));

        /// <summary>渠道详情（密钥掩码）。</summary>
        [HttpGet("{id:long}")]
        [ActionPermissionFilter(Permission = "ai:governance:query")]
        public IActionResult Get(long id) => SUCCESS(_service.Get(id));

        /// <summary>新增渠道。</summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "ai:governance:add")]
        [Log(Title = "AI模型渠道", BusinessType = BusinessType.INSERT)]
        public IActionResult Add([FromBody] AiProviderSaveDto input)
        {
            input.Id = 0;
            return SUCCESS(new { id = _service.Save(input) });
        }

        /// <summary>修改渠道（ApiKey 传掩码或留空表示不修改）。</summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "ai:governance:edit")]
        [Log(Title = "AI模型渠道", BusinessType = BusinessType.UPDATE)]
        public IActionResult Edit([FromBody] AiProviderSaveDto input) => SUCCESS(new { id = _service.Save(input) });

        /// <summary>删除渠道。</summary>
        [HttpDelete("{id:long}")]
        [ActionPermissionFilter(Permission = "ai:governance:remove")]
        [Log(Title = "AI模型渠道", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete(long id) => SUCCESS(_service.Delete(id));

        /// <summary>启用/停用渠道（0 启用 / 1 停用）。</summary>
        [HttpPut("{id:long}/status")]
        [ActionPermissionFilter(Permission = "ai:governance:edit")]
        [Log(Title = "AI模型渠道状态", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeStatus(long id, [FromQuery] int status) => SUCCESS(_service.ChangeStatus(id, status));

        /// <summary>
        /// 连通性测试：用当前表单值（可以是未保存内容）发一次最小请求。
        /// ApiKey 传掩码时自动用库中已保存的密钥（需带 Id）。
        /// </summary>
        [HttpPost("test")]
        [ActionPermissionFilter(Permission = "ai:governance:health")]
        public async Task<IActionResult> Test([FromBody] AiProviderTestInput input) => SUCCESS(await _service.TestAsync(input));

        /// <summary>
        /// 从上游拉取模型列表供下拉选择：入参同连通性测试（支持未保存的表单值，掩码 Key 自动用库中密钥）。
        /// 只回填前端，不写库；渠道未提供该接口时返回可行动提示（Ok=false）而不是报错。
        /// </summary>
        [HttpPost("models")]
        [ActionPermissionFilter(Permission = "ai:governance:health")]
        public async Task<IActionResult> Models([FromBody] AiProviderTestInput input) => SUCCESS(await _service.FetchModelsAsync(input));
    }
}
