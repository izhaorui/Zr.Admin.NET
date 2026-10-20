using ZR.Model.System.Dto;
using ZR.Model;
using ZR.Model.System.Tenant;

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
		/// 租户当前是否可用（未停用、未过期、未删除）。供 JwtAuthMiddleware 做状态复核，
		/// 弥补停服后存量 token 在有效期内仍可访问并自动续期的缺口。状态走 60 秒缓存。
		/// </summary>
		/// <param name="tenantId">租户标识</param>
		bool IsTenantActive(string tenantId);

		/// <summary>
		/// 通过租户标识查询租户信息。
		/// </summary>
		/// <param name="tenantId"></param>
		/// <returns></returns>
		SysTenant GetByTenantId(string tenantId);

		/// <summary>
		/// 获取域名→租户ID映射（平台级缓存）。键为租户 Domain 字段（小写，子域标签或完整域名），值为 TenantId。
		/// 供中间件按访问域名解析租户使用。
		/// </summary>
		Dictionary<string, string> GetDomainTenantMap();

		/// <summary>
		/// 清除域名→租户ID映射缓存（租户增改/停服/注销后调用）。
		/// </summary>
		void RemoveDomainMapCache();

		/// <summary>
		/// 校验域名(Domain)全局唯一。
		/// </summary>
		/// <param name="domain">待校验域名（子域标签或完整域名）</param>
		/// <param name="excludeId">排除的租户主键（编辑自身时传入）</param>
		/// <returns>UserConstants.UNIQUE / NOT_UNIQUE</returns>
		string CheckDomainUnique(string domain, long excludeId);

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
		/// 过期租户自动停服：扫描已到期且仍在启用的租户，逐个停服。供定时任务调用。
		/// </summary>
		/// <param name="operatorName">操作人，默认 system（定时任务）</param>
		/// <returns>实际停服的租户数量</returns>
		int SuspendExpiredTenants(string operatorName = "system");

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
		/// 踢出指定租户全部在线用户（按 MessageHub.OnlineClients 过滤该租户连接并发送强退通知）。
		/// 供停服/注销/停用后调用，推送失败仅记日志不影响主流程。返回通知到的连接数。
		/// </summary>
		/// <param name="tenantId">租户标识</param>
		/// <param name="reason">强退原因，展示给被踢用户</param>
		/// <returns></returns>
		int KickTenantOnlineUsers(string tenantId, string reason);

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

        /// <summary>
        /// 向全部/指定启用租户的管理员群发公告站内信。返回实际发送的租户数。
        /// </summary>
        /// <param name="dto">TenantIds 为空表示全部启用租户</param>
        /// <param name="operatorName"></param>
        /// <returns></returns>
        int BroadcastToTenants(TenantBroadcastDto dto, string operatorName);

        /// <summary>
        /// 到期前阶梯提醒：扫描启用且未过期的租户，在剩余天数命中 30/15/7/3/1 天阶梯时
        /// 向租户管理员推送站内信提醒。以 Remark 打标防同阶段重复发送。供定时任务调用。
        /// </summary>
        /// <param name="operatorName">操作人，默认 system（定时任务）</param>
        /// <returns>本次发送提醒的租户数量</returns>
        int RemindExpiringTenants(string operatorName = "system");

        /// <summary>
        /// 套餐到期降级通知：扫描已过期（EndTime &lt; now）且未处理的生效套餐绑定，向租户管理员
        /// 发送降级为默认套餐的站内信，绑定标记为已处理并写入 degrade 计费流水。供定时任务调用。
        /// </summary>
        /// <param name="operatorName">操作人，默认 system（定时任务）</param>
        /// <returns>本次处理降级通知的绑定数量</returns>
        int NotifyExpiredPlanBindings(string operatorName = "system");

        /// <summary>
        /// 按流水单号查询租户计费流水（支付回调/状态查询用）。
        /// </summary>
        /// <param name="orderNo">流水单号（TO 前缀）</param>
        /// <returns></returns>
        SysTenantOrder GetTenantOrderByNo(string orderNo);

        /// <summary>
        /// 将计费流水标记为已支付（回调幂等判断后调用）：置 PayStatus=1、渠道、交易号、支付时间。
        /// 仅 PayStatus=0 时生效（CAS），返回是否更新成功。
        /// </summary>
        /// <param name="orderNo">流水单号</param>
        /// <param name="transactionId">第三方交易号</param>
        /// <param name="payChannel">支付渠道，默认 wechat</param>
        /// <returns></returns>
        bool MarkTenantOrderPaid(string orderNo, string transactionId, string payChannel = "wechat");

        /// <summary>
        /// 创建租户在线续费待支付流水（支付闭环第一步）。
        /// 金额、时长、租户以流水为准，支付回调只认单号。
        /// </summary>
        /// <param name="tenantId">租户标识</param>
        /// <param name="durationDays">续费时长（天）</param>
        /// <param name="amount">应付金额（元）</param>
        /// <param name="operatorName">操作人</param>
        /// <returns>已落库的待支付流水（含单号）</returns>
        SysTenantOrder CreateTenantRenewOrder(string tenantId, int durationDays, decimal amount, string operatorName);
    }
}
