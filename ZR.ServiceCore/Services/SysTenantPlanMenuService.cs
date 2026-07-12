using Infrastructure;
using Infrastructure.Attribute;
using ZR.Common;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 套餐菜单服务
    /// </summary>
    [AppService(ServiceType = typeof(ISysTenantPlanMenuService), ServiceLifetime = LifeTime.Transient)]
    public class SysTenantPlanMenuService : BaseService<SysTenantPlanMenu>, ISysTenantPlanMenuService
    {
        private readonly ISysTenantService _sysTenantService;

        public SysTenantPlanMenuService(ISysTenantService sysTenantService)
        {
            _sysTenantService = sysTenantService;
        }

        #region 读取

        public List<long> GetMenuIdsByPlanCode(string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode))
                return new List<long>();

            return Context.Queryable<SysTenantPlanMenu>()
                .Where(x => x.PlanCode == planCode)
                .Select(x => x.MenuId)
                .Distinct()
                .ToList();
        }

        public List<long> GetMenuIdsByTenantId(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return new List<long>();

            var isMainTenant = string.Equals(tenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase);
            if (isMainTenant)
            {
                // 主租户默认拥有所有主库菜单
                return Context.Queryable<SysMenu>()
                    .Where(m => m.Status == "0")
                    .Select(m => m.MenuId)
                    .ToList();
            }

            var plan = _sysTenantService.GetCurrentTenantPlan(tenantId);
            if (plan == null || string.IsNullOrWhiteSpace(plan.PlanCode))
                return new List<long>();

            return GetMenuIdsByPlanCode(plan.PlanCode);
        }

        public List<string> GetPermsByTenantId(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return new List<string>();

            var cacheKey = $"tenant:plan:perms:{tenantId}";
            var cached = CacheHelper.GetCache(cacheKey) as List<string>;
            if (cached != null)
                return cached;

            var isMainTenant = string.Equals(tenantId, App.MainDbConfigId, StringComparison.OrdinalIgnoreCase);
            List<string> perms;
            if (isMainTenant)
            {
                perms = Context.Queryable<SysMenu>()
                    .Where(m => m.Status == "0" && !string.IsNullOrEmpty(m.Perms))
                    .Select(m => m.Perms)
                    .Distinct()
                    .ToList();
            }
            else
            {
                var menuIds = GetMenuIdsByTenantId(tenantId);
                if (menuIds.Count == 0)
                    return new List<string>();

                perms = Context.Queryable<SysMenu>()
                    .Where(m => menuIds.Contains(m.MenuId) && m.Status == "0" && !string.IsNullOrEmpty(m.Perms))
                    .Select(m => m.Perms)
                    .Distinct()
                    .ToList();
            }

            CacheHelper.SetCache(cacheKey, perms, 60 * 10);
            return perms;
        }

        public List<TenantMenuDto> GetPlanMenuTree(string planCode)
        {
            var menuIds = GetMenuIdsByPlanCode(planCode);
            var allMenus = Context.Queryable<SysMenu>()
                .Where(m => m.Status == "0" && new[] { "M", "C", "L", "F" }.Contains(m.MenuType))
                .OrderBy(m => new { m.ParentId, m.OrderNum })
                .ToList();

            var selectedSet = new HashSet<long>(menuIds);
            var list = allMenus.Select(m => new TenantMenuDto
            {
                Id = m.MenuId,
                MenuId = m.MenuId,
                ParentId = m.ParentId,
                MenuName = m.MenuName,
                CustomName = null,
                MenuType = m.MenuType,
                Path = m.Path,
                Perms = m.Perms,
                Icon = m.Icon,
                Component = m.Component,
                IsVisible = selectedSet.Contains(m.MenuId) ? 0 : 1,
                IsEnable = selectedSet.Contains(m.MenuId) ? 1 : 0,
                Sort = m.OrderNum,
                OrderNum = m.OrderNum,
                Status = m.Status
            }).ToList();

            var tree = BuildTree(list);
            // 容器节点（含子节点）的勾选状态由其子节点决定，不应单独标记为已选。
            // 否则前端 setCheckedKeys 会强制勾选其下所有子节点，导致无法取消子菜单勾选。
            ResetContainerCheckedFlag(tree);
            return tree;
        }

        #endregion

        #region 写入

        public int SavePlanMenus(string planCode, List<long> menuIds, string operatorName)
        {
            if (string.IsNullOrWhiteSpace(planCode))
                throw new CustomException("套餐编码不能为空");

            menuIds ??= new List<long>();
            var now = DateTime.Now;
            var entities = menuIds.Distinct().Select(menuId => new SysTenantPlanMenu
            {
                PlanCode = planCode,
                MenuId = menuId,
                Create_by = operatorName,
                Create_time = now
            }).ToList();

            var result = Context.Ado.UseTran(() =>
            {
                Context.Deleteable<SysTenantPlanMenu>()
                    .Where(x => x.PlanCode == planCode)
                    .ExecuteCommand();

                if (entities.Count > 0)
                {
                    Context.Insertable(entities).ExecuteCommand();
                }
            });

            if (!result.IsSuccess)
                throw new CustomException($"保存套餐菜单失败：{result.ErrorMessage}");

            ClearPlanCache(planCode);
            return entities.Count;
        }

        public int DeleteByPlanCode(string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode))
                return 0;

            ClearPlanCache(planCode);
            return Context.Deleteable<SysTenantPlanMenu>()
                .Where(x => x.PlanCode == planCode)
                .ExecuteCommand();
        }

        public int CopyPlanMenus(string sourcePlanCode, string targetPlanCode, string operatorName)
        {
            if (string.IsNullOrWhiteSpace(sourcePlanCode) || string.IsNullOrWhiteSpace(targetPlanCode))
                throw new CustomException("源套餐编码和目标套餐编码不能为空");

            var menuIds = GetMenuIdsByPlanCode(sourcePlanCode);
            return SavePlanMenus(targetPlanCode, menuIds, operatorName);
        }

        #endregion

        #region 私有方法

        private List<TenantMenuDto> BuildTree(List<TenantMenuDto> list)
        {
            var childrenLookup = list
                .GroupBy(m => m.ParentId)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Sort).ToList());

            List<TenantMenuDto> BuildChildren(long parentId)
            {
                if (!childrenLookup.TryGetValue(parentId, out var children))
                    return new List<TenantMenuDto>();

                foreach (var child in children)
                {
                    child.Children = BuildChildren(child.MenuId);
                }

                return children;
            }

            return BuildChildren(0);
        }

        /// <summary>
        /// 容器节点（含子节点）的勾选标记交由子节点驱动，强制置为未选，
        /// 避免前端 el-tree 在 setCheckedKeys 时把整个目录下的子菜单全部强制勾选。
        /// </summary>
        private void ResetContainerCheckedFlag(List<TenantMenuDto> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    node.IsEnable = 0;
                    node.IsVisible = 1;
                    ResetContainerCheckedFlag(node.Children);
                }
            }
        }

        private void ClearPlanCache(string planCode)
        {
            // 清除所有可能绑定该套餐的租户权限缓存
            var bindings = Context.Queryable<SysTenantPlanBinding>()
                .Where(x => x.PlanCode == planCode && x.DelFlag == 0 && x.Status == 0)
                .Select(x => x.TenantId)
                .Distinct()
                .ToList();

            foreach (var tenantId in bindings)
            {
                CacheHelper.Remove($"tenant:plan:perms:{tenantId}");
            }
        }

        #endregion
    }
}
