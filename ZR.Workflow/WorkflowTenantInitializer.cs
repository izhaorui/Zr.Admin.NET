using Microsoft.Extensions.Hosting;
using SqlSugar.IOC;
using ZR.ServiceCore.Services;
using ZR.ServiceCore.SqlSugar;

namespace ZR.Workflow
{
	/// <summary>
	/// 工作流模块租户级表初始化器。
	/// </summary>
	[AppService(ServiceType = typeof(ITenantModuleInitializer))]
	public class WorkflowTenantInitializer : ITenantModuleInitializer
	{
		public string ModuleName => "Workflow";

		public string InitializeTenant(string tenantId)
		{
			if (!App.IsTenantEnabled())
			{
				return "多租户未启用，跳过工作流表初始化";
			}

			if (string.IsNullOrWhiteSpace(tenantId))
			{
				throw new ArgumentException("租户标识不能为空", nameof(tenantId));
			}

			var db = DbScoped.SugarScope.GetConnectionScope(tenantId);

			InitCore(db);

			return $"工作流业务表初始化完成（{tenantId}）";
		}

		public void InitializeNonSaaS()
		{
			if (!InternalApp.WebHostEnvironment.IsDevelopment()) return;

			var db = DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);
			InitCore(db);
		}

		private static void InitCore(ISqlSugarClient db)
		{
			// 使用 DbMigrationService.EnsureEntitySchema：表不存在则建表，已存在则补齐缺失列
			// （CodeFirst.InitTables 不会给已存在的表加列）。幂等，不删列/不改类型。
			DbMigrationService.EnsureEntitySchema(db, typeof(WfFlowDefinition));
			DbMigrationService.EnsureEntitySchema(db, typeof(WfFlowInstance));
			DbMigrationService.EnsureEntitySchema(db, typeof(WfFlowNode));
			DbMigrationService.EnsureEntitySchema(db, typeof(WfFlowTask));
			DbMigrationService.EnsureEntitySchema(db, typeof(WfFlowRecord));
		}
	}
}
