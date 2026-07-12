using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Middleware;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 角色权限
    /// </summary>
    [AppService(ServiceType = typeof(ISysPermissionService), ServiceLifetime = LifeTime.Transient)]
    public class SysPermissionService : ISysPermissionService
    {
        private readonly ISysRoleService SysRoleService;
        private readonly ISysMenuService SysMenuService;
        private readonly ISysTenantPlanMenuService PlanMenuService;

        public SysPermissionService(
            ISysRoleService sysRoleService,
            ISysMenuService sysMenuService,
            ISysTenantPlanMenuService planMenuService)
        {
            SysRoleService = sysRoleService;
            SysMenuService = sysMenuService;
            PlanMenuService = planMenuService;
        }

        /// <summary>
        /// 获取角色数据权限
        /// </summary>
        /// <param name="user">用户信息</param>
        /// <returns>角色权限信息</returns>
        public List<string> GetRolePermission(SysUserDto user)
        {
            List<string> roles = new();
            // 管理员拥有所有权限
            if (user.IsAdmin)
            {
                roles.Add("admin");
            }
            else
            {
                roles.AddRange(SysRoleService.SelectUserRoleKeys(user.UserId));
            }
            return roles;
        }

        /// <summary>
        /// 获取菜单数据权限
        /// 多租户非主租户时按套餐菜单权限取交集过滤
        /// </summary>
        /// <param name="user">用户信息</param>
        /// <returns>菜单权限信息</returns>
        public List<string> GetMenuPermission(SysUserDto user)
        {
            List<string> perms = new();
            // 管理员拥有所有权限
            if (user.IsAdmin || GetRolePermission(user).Exists(f => f.Equals(GlobalConstant.AdminRole)))
            {
                perms.Add(GlobalConstant.AdminPerm);
            }
            else
            {
                perms.AddRange(SysMenuService.SelectMenuPermsByUserId(user.UserId));
            }

            // 多租户非主租户按套餐菜单权限过滤
            if (App.IsTenantEnabled() && !perms.Contains(GlobalConstant.AdminPerm))
            {
                var tenantId = App.GetCurrentTenantId();
                var isMainTenant = string.Equals(tenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase);
                if (!isMainTenant && !string.IsNullOrWhiteSpace(tenantId))
                {
                    var planPerms = PlanMenuService.GetPermsByTenantId(tenantId);
                    perms = perms.Intersect(planPerms, StringComparer.OrdinalIgnoreCase).ToList();
                }
            }

            return perms;
        }
    }
}
