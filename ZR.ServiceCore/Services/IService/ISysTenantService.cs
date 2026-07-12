using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model;

namespace ZR.ServiceCore.Services
{
	public interface ISysTenantService : IBaseService<SysTenant>
	{
		/// <summary>
		/// 分页查询租户。
		/// </summary>
		/// <param name="parm"></param>
		/// <returns></returns>
		PagedInfo<SysTenant> GetPageList(SysTenantQueryDto parm);

		/// <summary>
		/// 校验租户ID唯一。
		/// </summary>
		/// <param name="tenant"></param>
		/// <returns></returns>
		string CheckTenantIdUnique(SysTenant tenant);

		/// <summary>
		/// 登录前租户可用性校验。
		/// </summary>
		/// <param name="tenantId"></param>
		void CheckTenant(string tenantId);

		/// <summary>
		/// 通过租户标识查询租户信息。
		/// </summary>
		/// <param name="tenantId"></param>
		/// <returns></returns>
		SysTenant GetByTenantId(string tenantId);

		/// <summary>
		/// 租户开通
		/// </summary>
		/// <param name="dto"></param>
		/// <param name="operatorName"></param>
		/// <returns></returns>
		TenantLifecycleResult ProvisionTenant(TenantProvisionDto dto, string operatorName);

		/// <summary>
		/// 租户初始化
		/// </summary>
		/// <param name="dto"></param>
		/// <returns></returns>
		TenantLifecycleResult InitializeTenant(TenantInitializeDto dto);

		/// <summary>
		/// 租户停服
		/// </summary>
		/// <param name="tenantId"></param>
		/// <param name="operatorName"></param>
		/// <param name="remark"></param>
		/// <returns></returns>
		TenantLifecycleResult SuspendTenant(string tenantId, string operatorName, string remark = null);

		/// <summary>
		/// 租户续费
		/// </summary>
		/// <param name="dto"></param>
		/// <param name="operatorName"></param>
		/// <returns></returns>
		TenantLifecycleResult RenewTenant(TenantRenewDto dto, string operatorName);

		/// <summary>
		/// 租户删除
		/// </summary>
		/// <param name="dto"></param>
		/// <param name="operatorName"></param>
		/// <returns></returns>
		TenantLifecycleResult DecommissionTenant(TenantDecommissionDto dto, string operatorName);

		/// <summary>
		/// 套餐列表。
		/// </summary>
		/// <returns></returns>
		List<TenantPlanDto> GetTenantPlanList();

		/// <summary>
		/// 根据ID获取套餐。
		/// </summary>
		SysTenantPlan GetPlanById(long id);

		/// <summary>
		/// 根据编码获取套餐。
		/// </summary>
		SysTenantPlan GetPlanByCode(string planCode);

		/// <summary>
		/// 新增套餐。
		/// </summary>
		long InsertPlan(SysTenantPlan plan);

		/// <summary>
		/// 更新套餐。
		/// </summary>
		int UpdatePlan(SysTenantPlan plan);

		/// <summary>
		/// 删除套餐（软删除）。
		/// </summary>
		int DeletePlan(long id);

		/// <summary>
		/// 获取租户当前套餐信息。
		/// </summary>
		/// <param name="tenantId"></param>
		/// <returns></returns>
		TenantCurrentPlanDto GetCurrentTenantPlan(string tenantId);

		/// <summary>
		/// 分配租户套餐。
		/// </summary>
		/// <param name="dto"></param>
		/// <param name="operatorName"></param>
		/// <returns></returns>
		TenantCurrentPlanDto AssignTenantPlan(TenantPlanAssignDto dto, string operatorName);

		/// <summary>
		/// 新增用户前配额校验。
		/// </summary>
		/// <param name="tenantId"></param>
		/// <param name="addingCount"></param>
		void EnsureTenantUserQuotaForAdd(string tenantId, int addingCount = 1);

		/// <summary>
		/// 租户套餐用量面板。
		/// </summary>
		/// <param name="tenantId"></param>
		/// <returns></returns>
		TenantUsageDashboardDto GetTenantUsageDashboard(string tenantId);

        /// <summary>
        /// 登录页租户选择列表（仅返回正常状态的租户）。
        /// </summary>
        /// <returns></returns>
        List<TenantLoginInfoDto> GetLoginTenantList();

        /// <summary>
        /// 租户到期提醒列表。
        /// </summary>
        /// <param name="withinDays"></param>
        /// <returns></returns>
        List<TenantExpireReminderDto> GetTenantExpireReminders(int withinDays = 30);
    }
}
