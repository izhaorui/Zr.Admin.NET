using Infrastructure;
using Microsoft.Extensions.Hosting;
using SqlSugar;
using SqlSugar.IOC;
using System.Reflection;
using ZR.Mall.Model;
using ZR.Model;
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
			if (!App.OptionsSetting.InitMall) return;

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
			typeof(OMSPayment),
			typeof(MMSUserAddress),
			typeof(SpecTemplate),
		};

		private static void InitCore(ISqlSugarClient db)
		{
			// 先确保表存在（表不存在时建表），再对已有表显式补齐缺失的列。
			// CodeFirst.InitTables 对已存在的表不会 ALTER 加列，故这里单独做列差异补列，
			// 保证 PayType 等后加字段在老表上也能被追加。
			foreach (var entityType in MallEntityTypes)
			{
				EnsureEntitySchema(db, entityType);
			}
		}

		/// <summary>
		/// 确保单个商城实体对应的表结构：表不存在则建表；表已存在则补齐实体中新增的列。
		/// 幂等，不会删除列或修改列类型。
		/// </summary>
		internal static void EnsureEntitySchema(ISqlSugarClient db, Type entityType)
		{
			var entityInfo = db.EntityMaintenance.GetEntityInfo(entityType);
			var tableName = entityInfo.DbTableName;

			if (!db.DbMaintenance.IsAnyTable(tableName, false))
			{
				db.CodeFirst.InitTables(entityType);
				return;
			}

			var existingColumns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false)
				.Select(c => c.DbColumnName.ToLowerInvariant())
				.ToHashSet();

			foreach (var col in entityInfo.Columns)
			{
				if (col.IsIgnore) continue;
				if (existingColumns.Contains(col.DbColumnName.ToLowerInvariant())) continue;

				// 与框架 SqlsugarSetup.EntityService 约定保持一致：仅当属性显式标记
				// [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)] 时该列才是 NOT NULL，
				// 其余一律按可空处理。GetEntityInfo() 返回的 col.IsNullable 不会执行 EntityService
				// 回调，取值不可靠，直接用特性判断，避免生成非法的 "... NOT NULL DEFAULT NULL"。
				var prop = entityType.GetProperty(col.PropertyName);
				var sugarAttr = prop?.GetCustomAttribute<SugarColumn>();
				var isNotNull = sugarAttr?.ExtendedAttribute?.ToString() == ProteryConstant.NOTNULL.ToString();

				try
				{
					// SqlSugar 对可空列无 DefaultValue 特性时会填充字符串 "NULL"，
					// 直接传给 AddColumn 会生成非法 SQL "... DEFAULT NULL" 导致 MySQL 报错。
					// 归一为空，让 SqlSugar 跳过 DEFAULT 子句。
					var defaultValue = col.DefaultValue;
					if (string.IsNullOrWhiteSpace(defaultValue)
						|| defaultValue.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
					{
						defaultValue = null;
					}

				db.DbMaintenance.AddColumn(tableName, new DbColumnInfo
				{
					DbColumnName = col.DbColumnName,
					DataType = DbMigrationService.ResolveColumnDataType(db, col, prop),
					IsNullable = !isNotNull,
					DefaultValue = defaultValue
				});
					Log.WriteLine(ConsoleColor.Green, $"[商城迁移] 表 {tableName} 已补列 {col.DbColumnName} {col.DataType} {(isNotNull ? "NOT NULL" : "NULL")}");
				}
				catch (Exception ex)
				{
					// 单个列补列失败（如 NOT NULL 且无默认值）不中断其他表/列，打印提示交由人工处理
					Log.WriteLine(ConsoleColor.Yellow, $"[商城迁移] 表 {tableName} 补列 {col.DbColumnName} 失败: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// 计算商城实体与数据库当前结构的差异（仅预览，不执行 DDL），供前端展示。
		/// </summary>
		internal static MigrationReport ComputeMallDiff(ISqlSugarClient db)
		{
			var report = new MigrationReport();
			var dbSchema = DbMigrationService.GetDbSchemaForConnection(db);

			foreach (var entityType in MallEntityTypes)
			{
				var entityInfo = db.EntityMaintenance.GetEntityInfo(entityType);
				var tableName = entityInfo.DbTableName;
				var normalizedName = tableName.ToLowerInvariant();

				if (!dbSchema.Tables.TryGetValue(normalizedName, out var existingTable))
				{
					report.NewTables.Add(tableName);
					continue;
				}

				foreach (var col in entityInfo.Columns)
				{
					if (col.IsIgnore) continue;
					if (existingTable.Columns.Contains(col.DbColumnName.ToLowerInvariant())) continue;

					var tc = report.NewColumns.FirstOrDefault(c => c.TableName == tableName);
					if (tc == null)
					{
						tc = new TableColumnChange { TableName = tableName };
						report.NewColumns.Add(tc);
					}
					tc.Columns.Add(col.DbColumnName);
				}
			}

			report.Success = true;
			return report;
		}
	}
}
