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
	}
}
