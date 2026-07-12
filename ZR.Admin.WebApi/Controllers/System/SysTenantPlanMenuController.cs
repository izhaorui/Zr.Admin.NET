using Microsoft.AspNetCore.Mvc;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 套餐菜单管理
    /// </summary>
    [Route("system/tenantPlanMenu")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysTenantPlanMenuController : BaseController
    {
        private readonly ISysTenantPlanMenuService _planMenuService;
        private readonly ISysTenantService _sysTenantService;
        private readonly ISysMenuService _sysMenuService;

        public SysTenantPlanMenuController(
            ISysTenantPlanMenuService planMenuService,
            ISysTenantService sysTenantService,
            ISysMenuService sysMenuService)
        {
            _planMenuService = planMenuService;
            _sysTenantService = sysTenantService;
            _sysMenuService = sysMenuService;
        }

        /// <summary>
        /// 获取套餐菜单树（供勾选）
        /// </summary>
        [HttpGet("tree")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult GetPlanMenuTree(string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode))
                return ToResponse(ResultCode.FAIL, "套餐编码不能为空");

            var tree = _planMenuService.GetPlanMenuTree(planCode);
            return SUCCESS(tree);
        }

        /// <summary>
        /// 获取当前租户套餐菜单树（子租户查看本租户菜单）
        /// </summary>
        [HttpGet("myTree")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult GetMyPlanMenuTree()
        {
            if (!App.IsTenantEnabled())
                return ToResponse(ResultCode.FAIL, "多租户未启用");

            var tenantId = App.GetCurrentTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
                return ToResponse(ResultCode.FAIL, "无法获取当前租户标识");

            var plan = _sysTenantService.GetCurrentTenantPlan(tenantId);
            if (plan == null || string.IsNullOrWhiteSpace(plan.PlanCode))
                return SUCCESS(new List<TenantMenuDto>());

            var tree = _planMenuService.GetPlanMenuTree(plan.PlanCode);
            return SUCCESS(tree);
        }

        /// <summary>
        /// 保存套餐菜单（全量替换）
        /// </summary>
        [HttpPost("save")]
        [Log(Title = "套餐菜单配置", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        public IActionResult SavePlanMenus([FromBody] TenantPlanMenuSaveDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.PlanCode))
                return ToResponse(ResultCode.FAIL, "套餐编码不能为空");

            var count = _planMenuService.SavePlanMenus(dto.PlanCode, dto.MenuIds, HttpContext.GetName());
            return SUCCESS($"已保存 {count} 条菜单关联");
        }

        /// <summary>
        /// 复制套餐菜单
        /// </summary>
        [HttpPost("copy")]
        [Log(Title = "套餐菜单复制", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "system:tenant:update")]
        public IActionResult CopyPlanMenus([FromBody] TenantPlanMenuCopyDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SourcePlanCode) || string.IsNullOrWhiteSpace(dto.TargetPlanCode))
                return ToResponse(ResultCode.FAIL, "源套餐编码和目标套餐编码不能为空");

            var count = _planMenuService.CopyPlanMenus(dto.SourcePlanCode, dto.TargetPlanCode, HttpContext.GetName());
            return SUCCESS($"已复制 {count} 条菜单关联");
        }

        /// <summary>
        /// 获取套餐菜单权限列表（供套餐能力展示）
        /// </summary>
        [HttpGet("perms")]
        [ActionPermissionFilter(Permission = "system:tenant:list")]
        public IActionResult GetPlanPerms(string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode))
                return ToResponse(ResultCode.FAIL, "套餐编码不能为空");

            var menuIds = _planMenuService.GetMenuIdsByPlanCode(planCode);
            var menus = _sysMenuService.SelectMenuList(new MenuQueryDto { MenuTypeIds = "M,C,F,L" }, 0)
                .Where(m => menuIds.Contains(m.MenuId) && !string.IsNullOrEmpty(m.Perms))
                .Select(m => m.Perms)
                .Distinct()
                .ToList();
            return SUCCESS(menus);
        }
    }

    /// <summary>
    /// 套餐菜单保存请求
    /// </summary>
    public class TenantPlanMenuSaveDto
    {
        public string PlanCode { get; set; }
        public List<long> MenuIds { get; set; } = new List<long>();
    }

    /// <summary>
    /// 套餐菜单复制请求
    /// </summary>
    public class TenantPlanMenuCopyDto
    {
        public string SourcePlanCode { get; set; }
        public string TargetPlanCode { get; set; }
    }
}
