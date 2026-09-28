using Infrastructure;
using SqlSugar.IOC;
using ZR.Mall.Model;
using ZR.Model.Member;
using ZR.ServiceCore.Services;
using ZR.ServiceCore.SqlSugar;

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
			typeof(UmsMember),
			typeof(SpecTemplate),
		};

		/// <summary>
		/// 会员自增起始值：不对外暴露 1、2、3 这类连续小号（规则说明与既有库一次性 SQL 见
		/// document/doc/商城会员接口对接文档.md）。
		/// 注意：CodeFirst 建表默认 IDENTITY(1,1)，且 EnsureEntitySchemas 只建表/补列、不改自增种子，
		/// 因此必须在此显式校正。
		/// </summary>
		private const long MemberIdStart = 100000;

		private static void InitCore(ISqlSugarClient db)
		{
			// 表不存在则建表，已存在则补齐缺失列（CodeFirst.InitTables 不会给已有表加列）。
			DbMigrationService.EnsureEntitySchemas(db, MallEntityTypes);
			EnsureMemberIdSeed(db);
		}

		/// <summary>
		/// 校正 ums_member 的自增种子（幂等）。
		/// 仅当库内最大 Id 小于起始值时执行：DBCC CHECKIDENT 会直接改写种子，若库中已有大于等于
		/// 起始值的记录，重置后将与既有主键冲突，故必须守卫"只向上补"。
		/// 表名/列名取自 SqlSugar 元数据（项目列命名并非统一 snake_case），不硬编码。
		/// 不支持的数据库仅记录日志提示手工处理，不阻断初始化。
		/// </summary>
		private static void EnsureMemberIdSeed(ISqlSugarClient db)
		{
			try
			{
				var entityInfo = db.EntityMaintenance.GetEntityInfo<UmsMember>();
				var table = entityInfo.DbTableName;
				var column = entityInfo.Columns.FirstOrDefault(c => c.PropertyName == nameof(UmsMember.Id))?.DbColumnName
					?? nameof(UmsMember.Id);

				// 空表时 MAX 为 NULL，用 GetScalar 在 C# 侧归一，避免 ISNULL/IFNULL 的方言差异
				var maxObj = db.Ado.GetScalar($"SELECT MAX({column}) FROM {table}");
				var maxId = maxObj == null || maxObj == DBNull.Value ? 0L : Convert.ToInt64(maxObj);
				if (maxId >= MemberIdStart)
				{
					return;
				}

				switch (db.CurrentConnectionConfig.DbType)
				{
					case DbType.SqlServer:
						// RESEED 传 N 表示下一条为 N + 1
						db.Ado.ExecuteCommand($"DBCC CHECKIDENT('{table}', RESEED, {MemberIdStart - 1})");
						break;
					case DbType.MySql:
						db.Ado.ExecuteCommand($"ALTER TABLE {table} AUTO_INCREMENT = {MemberIdStart}");
						break;
					default:
						Log.WriteLine(ConsoleColor.Yellow,
							$"[Mall] 会员ID起始值：当前数据库({db.CurrentConnectionConfig.DbType})不支持自动校正，请按部署文档手工把 {table} 的自增起始值设为 {MemberIdStart}");
						return;
				}

				Log.WriteLine(ConsoleColor.Green, $"[Mall] 会员ID自增起始值已校正为 {MemberIdStart}（{table}）");
			}
			catch (Exception ex)
			{
				// 校正失败不影响建表与业务可用性（最坏情况仍从 1 起），仅记录待人工处理
				Log.WriteLine(ConsoleColor.Yellow, $"[Mall] 会员ID起始值校正失败（忽略）：{ex.Message}");
			}
		}
	}
}
