using SqlSugar.IOC;
using ZR.Mall.Model;
using ZR.ServiceCore.Services;
using ZR.ServiceCore.SqlSugar;

namespace ZR.Mall
{
	/// <summary>
	/// 商城模块租户级表初始化器。
	/// 会员表（ums_member）的建表与自增种子校正已移交 ZR.ServiceCore.Member.MemberTenantInitializer，
	/// 商城不再自持会员能力，仅保留商城业务实体。
	/// </summary>
	/// <remarks>
	/// 放项目根而非 <c>Service/</c>：它是启动引导（实现 Infrastructure 的框架契约、只在建库/建租户时被调度），
	/// 不是业务服务；与 <c>ZR.Workflow</c> 的同名初始化器归属一致。
	/// </remarks>
	[AppService(ServiceType = typeof(ITenantModuleInitializer))]
	public class MallTenantInitializer : ITenantModuleInitializer
	{
		public string ModuleName => "Mall";

		public string InitializeTenant(string tenantId)
		{
			if (!App.IsTenantEnabled())
			{
				return "多租户未启用，跳过商城表初始化";
			}

			if (string.IsNullOrWhiteSpace(tenantId))
			{
				throw new ArgumentException("租户标识不能为空", nameof(tenantId));
			}

			var db = DbScoped.SugarScope.GetConnectionScope(tenantId);

			InitCore(db);

			return $"商城业务表初始化完成（{tenantId}）";
		}

		public void InitializeNonSaaS()
		{
			if (!App.OptionsSetting.ModuleInit.Mall) return;

			var db = DbScoped.SugarScope.GetConnectionScope(App.MallDbConfigId);
			InitCore(db);
		}

		internal static readonly Type[] MallEntityTypes =
		{
			typeof(Product),
			typeof(ProductSpec),
			typeof(Skus),
			typeof(Category),
			typeof(Brand),
			typeof(OMSOrder),
			typeof(OMSOrderItem),
			typeof(OMSOrderLog),
			typeof(OMSPayment),
			typeof(MMSUserAddress),
			typeof(SpecTemplate),
			// 运费模板与不配送地区：订单运费的数据来源
			typeof(FreightTemplate),
			typeof(FreightTemplateExcludeRegion),
			// 售后单（仅退款 / 退货退款）
			typeof(OMSAfterSale)
		};

		private static void InitCore(ISqlSugarClient db)
		{
			// 表不存在则建表，已存在则补齐缺失列（CodeFirst.InitTables 不会给已有表加列）。
			DbMigrationService.EnsureEntitySchemas(db, MallEntityTypes);
		}
	}
}
