using Microsoft.Extensions.Hosting;
using SqlSugar.IOC;
using ZR.Mall.Model;
using ZR.ServiceCore.Services;

namespace ZR.Mall.Service
{
	/// <summary>
	/// 商城模块租户级表初始化器。
	/// </summary>
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
			if (!App.OptionsSetting.InitDb) return;
			if (!InternalApp.WebHostEnvironment.IsDevelopment()) return;

			var db = DbScoped.SugarScope.GetConnectionScope(App.MallDbConfigId);
			InitCore(db);
		}

		private static void InitCore(ISqlSugarClient db)
		{
			db.CodeFirst.InitTables(typeof(Product));
			db.CodeFirst.InitTables(typeof(ProductSpec));
			db.CodeFirst.InitTables(typeof(Skus));
			db.CodeFirst.InitTables(typeof(Category));
			db.CodeFirst.InitTables(typeof(Brand));
			db.CodeFirst.InitTables(typeof(OMSOrder));
			db.CodeFirst.InitTables(typeof(OMSOrderItem));
			db.CodeFirst.InitTables(typeof(MMSUserAddress));
			db.CodeFirst.InitTables(typeof(SpecTemplate));
		}
	}
}
