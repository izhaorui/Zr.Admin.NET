using Microsoft.AspNetCore.Mvc;
using ZR.Model.System.Dto;
using ZR.Model.System.Tenant;

namespace ZR.Admin.WebApi.Controllers.System.tenant
{
    /// <summary>
    /// 套餐管理
    /// </summary>
    [Route("system/tenantPlan")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysTenantPlanController : BaseController
    {
        private readonly ISysTenantService _sysTenantService;

        public SysTenantPlanController(ISysTenantService sysTenantService)
        {
            _sysTenantService = sysTenantService;
        }

        /// <summary>
        /// 查询套餐列表。
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult PlanList()
        {
            return SUCCESS(_sysTenantService.GetTenantPlanList());
        }

        /// <summary>
        /// 获取套餐详情。
        /// </summary>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult PlanDetail(long id)
        {
            var plan = _sysTenantService.GetPlanById(id);
            if (plan == null)
                return ToResponse(ResultCode.FAIL, "套餐不存在");
            return SUCCESS(plan);
        }

        /// <summary>
        /// 新增套餐。
        /// </summary>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "套餐管理", BusinessType = BusinessType.INSERT)]
        public IActionResult AddPlan([FromBody] SysTenantPlan plan)
        {
            var id = _sysTenantService.InsertPlan(plan);
            return SUCCESS(new { id });
        }

        /// <summary>
        /// 编辑套餐。
        /// </summary>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "套餐管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult EditPlan([FromBody] SysTenantPlan plan)
        {
            _sysTenantService.UpdatePlan(plan);
            return SUCCESS(1);
        }

        /// <summary>
        /// 删除套餐。
        /// </summary>
        [HttpDelete("{id}")]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "套餐管理", BusinessType = BusinessType.DELETE)]
        public IActionResult DeletePlan(long id)
        {
            _sysTenantService.DeletePlan(id);
            return SUCCESS(1);
        }

        /// <summary>
        /// 查询租户当前套餐。
        /// </summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        [HttpGet("current")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult CurrentPlan(string tenantId)
        {
            return SUCCESS(_sysTenantService.GetCurrentTenantPlan(tenantId));
        }

        /// <summary>
        /// 查询租户套餐用量面板。
        /// </summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        [HttpGet("usage")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult Usage(string tenantId)
        {
            return SUCCESS(_sysTenantService.GetTenantUsageDashboard(tenantId));
        }

        /// <summary>
        /// 分配租户套餐。
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost("assign")]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        [Log(Title = "租户套餐分配", BusinessType = BusinessType.UPDATE)]
        public IActionResult AssignPlan([FromBody] TenantPlanAssignDto dto)
        {
            if (dto == null)
            {
                throw new CustomException("请求参数错误");
            }

            return SUCCESS(_sysTenantService.AssignTenantPlan(dto, HttpContext.GetName()));
        }
    }
}
