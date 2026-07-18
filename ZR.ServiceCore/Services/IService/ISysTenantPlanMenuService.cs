using ZR.Model.System.Dto;
using ZR.Model.System.Tenant;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 套餐菜单服务接口
    /// </summary>
    public interface ISysTenantPlanMenuService : IBaseService<SysTenantPlanMenu>
    {
        /// <summary>
        /// 获取套餐关联的菜单ID列表
        /// </summary>
        List<long> GetMenuIdsByPlanCode(string planCode);

        /// <summary>
        /// 根据租户当前套餐获取菜单ID列表
        /// </summary>
        List<long> GetMenuIdsByTenantId(string tenantId);

        /// <summary>
        /// 获取套餐关联的权限字符串列表（用于缓存）
        /// </summary>
        List<string> GetPermsByTenantId(string tenantId);

        /// <summary>
        /// 获取套餐菜单树（合并主库菜单信息）
        /// </summary>
        List<TenantMenuDto> GetPlanMenuTree(string planCode);

        /// <summary>
        /// 保存套餐菜单（全量替换）
        /// </summary>
        int SavePlanMenus(string planCode, List<long> menuIds, string operatorName);

        /// <summary>
        /// 删除套餐下所有菜单关联
        /// </summary>
        int DeleteByPlanCode(string planCode);

        /// <summary>
        /// 复制套餐菜单
        /// </summary>
        int CopyPlanMenus(string sourcePlanCode, string targetPlanCode, string operatorName);
    }
}
