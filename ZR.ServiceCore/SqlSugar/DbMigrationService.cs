using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using SqlSugar.IOC;
using System.Reflection;
using System.Text.Json;
using ZR.Model;
using ZR.Model.Content;
using ZR.Model.Models;
using ZR.Model.Public;
using ZR.Model.System;
using ZR.Model.System.Generate;
using ZR.Model.System.Model;
using ZR.Model.System.Tenant;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 数据库迁移服务 —— 显式实体注册表 + 差异报告 + 历史记录 + 单实体容错。
    /// 不扫描程序集，只迁移 SystemEntityTypes 数组中显式注册的类型。
    /// </summary>
    public static class DbMigrationService
    {
        /// <summary>
        /// 系统实体注册表（显式列出所有需要自动迁移的实体类型）。
        /// 新增表时在这里加一行 typeof(YourEntity), 即可。
        /// </summary>
        private static readonly Type[] SystemEntityTypes =
        {
            typeof(SysUser),
            typeof(SysRole),
            typeof(SysDept),
            typeof(SysPost),
            typeof(SysFile),
            typeof(SysConfig),
            typeof(SysNotice),
            typeof(SysLogininfor),
            typeof(SysOperLog),
            typeof(SysMenu),
            typeof(SysRoleMenu),
            typeof(SysRoleDept),
            typeof(SysUserRole),
            typeof(SysUserPost),
            typeof(SysTasks),
            typeof(SysTasksLog),
            typeof(SysTenant),
            typeof(SysTenantPlan),
            typeof(SysTenantPlanBinding),
            typeof(SysTenantPlanMenu),
            typeof(CommonLang),
            typeof(GenTable),
            typeof(GenTableColumn),
            typeof(SysDictData),
            typeof(SysDictType),
            typeof(SqlDiffLog),
            typeof(EmailTpl),
            typeof(SmsCodeLog),
            typeof(EmailLog),
            typeof(Article),
            typeof(ArticleCategory),
            typeof(ArticleBrowsingLog),
            typeof(ArticlePraise),
            typeof(ArticleComment),
            typeof(ArticleTopic),
            typeof(BannerConfig),
            typeof(SysUserMsg),
            typeof(SysFileGroup),
            typeof(DailySchedule),
        };

        /// <summary>
        /// 获取本次迁移实际使用的实体类型列表：
        /// 系统注册表 + 配置文件 AdditionalTypes - 被 [SkipMigration] 排除的实体。
        /// </summary>
        private static List<Type> ResolveEntityTypes(IReadOnlyList<Type> systemTypes, string[] additionalTypeNames, out List<string> skipped)
        {
            skipped = new List<string>();
            var result = new List<Type>();

            // 系统注册表实体
            foreach (var t in systemTypes)
            {
                var skipAttr = t.GetCustomAttribute<SkipMigrationAttribute>();
                if (skipAttr != null)
                {
                    skipped.Add($"{t.Name} ({skipAttr.Reason ?? "未指定原因"})");
                    continue;
                }
                result.Add(t);
            }

            // 配置文件 AdditionalTypes（可选扩展）
            if (additionalTypeNames != null)
            {
                foreach (var typeName in additionalTypeNames)
                {
                    if (string.IsNullOrWhiteSpace(typeName)) continue;
                    var t = Type.GetType(typeName, throwOnError: false);
                    if (t == null)
                    {
                        skipped.Add($"{typeName} (类型未找到)");
                        continue;
                    }
                    if (t.GetCustomAttribute<SkipMigrationAttribute>() != null ||
                        t.GetCustomAttribute<SugarTable>() == null)
                    {
                        skipped.Add($"{t.Name} (缺少 [SugarTable] 或标记了 [SkipMigration])");
                        continue;
                    }
                    result.Add(t);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取当前数据库中的所有表和列快照，用于迁移前后对比
        /// </summary>
        public static DbSchemaSnapshot GetDbSchema(SqlSugarScope db)
        {
            var snapshot = new DbSchemaSnapshot();
            try
            {
                var tables = db.DbMaintenance.GetTableInfoList(false);
                foreach (var table in tables)
                {
                    var tableInfo = new TableInfo { Name = table.Name.ToLowerInvariant() };
                    try
                    {
                        var columns = db.DbMaintenance.GetColumnInfosByTableName(table.Name, false);
                        tableInfo.Columns = columns.Select(c => c.DbColumnName.ToLowerInvariant()).ToHashSet();
                    }
                    catch { /* 表可能被删除，跳过 */ }
                    snapshot.Tables[tableInfo.Name] = tableInfo;
                }
            }
            catch (Exception ex)
            {
                snapshot.Error = ex.Message;
            }
            return snapshot;
        }

        /// <summary>
        /// 执行 CodeFirst 迁移（建新表 + 补新列），返回变更报告
        /// </summary>
        public static MigrationReport Migrate(SqlSugarScope db, IWebHostEnvironment env)
        {
            var report = new MigrationReport();
            var options = App.OptionsSetting;
            var reportOnly = options.DbMigration?.ReportOnly ?? false;
            var additionalTypes = options.DbMigration?.AdditionalTypes;

            // 1) 确保迁移历史表存在（自举）
            EnsureMigrationHistoryTable(db);

            // 2) 解析实体列表（系统注册表 + 配置文件扩展 - [SkipMigration] 排除）
            var entities = ResolveEntityTypes(SystemEntityTypes, additionalTypes, out var skipped);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[DbMigration] 注册实体 {SystemEntityTypes.Length} 个，实际迁移 {entities.Count} 个");
            if (additionalTypes is { Length: > 0 })
            {
                Console.WriteLine($"[DbMigration] 配置文件额外类型: {string.Join(", ", additionalTypes)}");
            }
            if (skipped.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                foreach (var s in skipped)
                {
                    Console.WriteLine($"[DbMigration]   [跳过] {s}");
                }
            }
            Console.ResetColor();

            // 3) 迁移前快照
            var beforeSchema = GetDbSchema(db);

            // 4) ReportOnly 模式：对比实体模型与数据库实际结构，不执行 DDL
            if (reportOnly)
            {
                ComputeEntityVsDbDiff(entities, beforeSchema, report);
                report.Success = true;
                PrintReport(report);
                return report;
            }

            // 5) 执行 CodeFirst 迁移（逐个实体，单个失败不影响其他）
            var batchId = $"{DateTime.Now:yyyyMMddHHmmss}_{Random.Shared.Next(1000, 9999)}";
            var migrationErrors = new List<string>();

            StaticConfig.CodeFirst_MySqlCollate = "utf8mb4_general_ci";

            // 建库（如不存在）
            db.DbMaintenance.CreateDatabase();

            foreach (var entityType in entities)
            {
                try
                {
                    db.CodeFirst.InitTables(entityType);
                }
                catch (Exception ex)
                {
                    migrationErrors.Add($"{entityType.Name}: {ex.Message}");
                }
            }

            report.Success = true;
            if (migrationErrors.Count > 0)
            {
                report.FailedEntities = migrationErrors;
            }

            // 6) 迁移后快照 & 计算差异
            var afterSchema = GetDbSchema(db);
            ComputeDiff(beforeSchema, afterSchema, report);

            // 7) 记录迁移历史
            SaveMigrationHistory(db, batchId, report);

            // 8) 打印报告
            PrintReport(report);

            return report;
        }

        /// <summary>
        /// 确保迁移历史表存在（自举建表）
        /// </summary>
        private static void EnsureMigrationHistoryTable(SqlSugarScope db)
        {
            try
            {
                if (db.DbMaintenance.IsAnyTable("__db_migration_history", false))
                    return;
                db.CodeFirst.InitTables(typeof(DbMigrationHistory));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DbMigration] 创建迁移历史表失败（不影响主流程）: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// 计算前后快照差异（迁移后使用）
        /// </summary>
        private static void ComputeDiff(DbSchemaSnapshot before, DbSchemaSnapshot after, MigrationReport report)
        {
            if (before.Error != null || after.Error != null)
            {
                report.DiffError = before.Error ?? after.Error;
                return;
            }

            foreach (var kvp in after.Tables)
            {
                if (!before.Tables.ContainsKey(kvp.Key))
                {
                    report.NewTables.Add(kvp.Key);
                }
            }

            foreach (var kvp in after.Tables)
            {
                if (!before.Tables.TryGetValue(kvp.Key, out var beforeTable))
                    continue;

                var newCols = kvp.Value.Columns
                    .Where(c => !beforeTable.Columns.Contains(c))
                    .ToList();

                if (newCols.Count > 0)
                {
                    report.NewColumns.Add(new TableColumnChange
                    {
                        TableName = kvp.Key,
                        Columns = newCols.ToHashSet()
                    });
                }
            }
        }

        /// <summary>
        /// ReportOnly 模式：将实体模型与数据库实际结构对比，预测变更
        /// </summary>
        private static void ComputeEntityVsDbDiff(List<Type> entityTypes, DbSchemaSnapshot dbSchema, MigrationReport report)
        {
            foreach (var entityType in entityTypes)
            {
                var tableAttr = entityType.GetCustomAttribute<SugarTable>();
                var tableName = tableAttr?.TableName ?? entityType.Name;
                var normalizedName = tableName.ToLowerInvariant();

                if (!dbSchema.Tables.TryGetValue(normalizedName, out var existingTable))
                {
                    report.NewTables.Add(tableName);
                    continue;
                }

                var entityProps = entityType.GetProperties()
                    .Where(p => p.CanWrite && p.GetCustomAttribute<SugarColumn>()?.IsIgnore != true)
                    .ToList();

                foreach (var prop in entityProps)
                {
                    var columnAttr = prop.GetCustomAttribute<SugarColumn>();
                    if (columnAttr?.IsIgnore == true) continue;
                    if (columnAttr == null && !IsSimpleType(prop.PropertyType)) continue;

                    var colName = columnAttr?.ColumnName ?? ToSnakeCase(prop.Name);
                    var normalizedColName = colName.ToLowerInvariant();

                    if (!existingTable.Columns.Contains(normalizedColName))
                    {
                        var tc = report.NewColumns.FirstOrDefault(c => c.TableName == tableName);
                        if (tc == null)
                        {
                            tc = new TableColumnChange { TableName = tableName };
                            report.NewColumns.Add(tc);
                        }
                        tc.Columns.Add(colName);
                    }
                }
            }
        }

        private static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum
                || type == typeof(string) || type == typeof(decimal)
                || type == typeof(DateTime) || type == typeof(DateTimeOffset)
                || type == typeof(Guid) || type == typeof(TimeSpan);
        }

        private static string ToSnakeCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            return sb.ToString();
        }

        private static void SaveMigrationHistory(SqlSugarScope db, string batchId, MigrationReport report)
        {
            try
            {
                var totalNewCols = report.NewColumns.Sum(c => c.Columns.Count);
                var summary = report.HasChanges
                    ? $"新增表 {report.NewTables.Count} 张，新增列 {totalNewCols} 个"
                    : "无变更";
                if (report.HasFailures)
                    summary += $"，失败 {report.FailedEntities.Count} 个实体";

                var details = System.Text.Json.JsonSerializer.Serialize(new
                {
                    report.NewTables,
                    NewColumns = report.NewColumns.Select(c => new { c.TableName, c.Columns }).ToList(),
                    report.FailedEntities
                });

                db.Insertable(new DbMigrationHistory
                {
                    BatchId = batchId,
                    Summary = summary,
                    Details = details,
                    AppliedAt = DateTime.Now,
                    NewTables = report.NewTables.Count,
                    NewColumns = totalNewCols,
                    Success = report.Success,
                    Error = Truncate(report.Error, 3900)
                }).ExecuteCommand();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DbMigration] 记录迁移历史失败: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void PrintReport(MigrationReport report)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========== 数据库迁移报告 ==========");
            Console.ResetColor();

            // 全盘致命错误
            if (!report.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"迁移中断: {report.Error}");
                Console.ResetColor();
                return;
            }

            // 无任何变更也无错误
            if (!report.HasChanges && !report.HasFailures)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("数据库结构与实体模型一致，无需变更。");
                Console.ResetColor();
                Console.WriteLine("====================================");
                return;
            }

            // 差异变更
            if (report.NewTables.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"--- 新增表 ({report.NewTables.Count}) ---");
                foreach (var t in report.NewTables)
                    Console.WriteLine($"  + {t}");
            }

            if (report.NewColumns.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"--- 新增列 ({report.NewColumns.Sum(c => c.Columns.Count)}) ---");
                foreach (var tc in report.NewColumns)
                {
                    Console.WriteLine($"  [{tc.TableName}]");
                    foreach (var col in tc.Columns)
                        Console.WriteLine($"    + {col}");
                }
            }

            // 部分实体失败
            if (report.HasFailures)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"--- DDL 失败 ({report.FailedEntities.Count}) ---");
                foreach (var err in report.FailedEntities)
                    Console.WriteLine($"  ! {err}");
            }

            Console.ForegroundColor = report.HasFailures ? ConsoleColor.Yellow : ConsoleColor.Green;
            var status = report.HasFailures ? "部分成功" : "成功";
            Console.WriteLine($"迁移{status}: 新增 {report.NewTables.Count} 表, {report.NewColumns.Sum(c => c.Columns.Count)} 列"
                + (report.HasFailures ? $", 失败 {report.FailedEntities.Count} 个实体" : ""));
            Console.ResetColor();
            Console.WriteLine("====================================");

            if (report.DiffError != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"注意: 差异计算异常 ({report.DiffError})，以上报告可能不完整");
                Console.ResetColor();
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return value[..maxLength];
        }
    }

    #region 辅助类型

    public class MigrationReport
    {
        public List<string> NewTables { get; set; } = new();
        public List<TableColumnChange> NewColumns { get; set; } = new();
        /// <summary>迁移是否成功执行（true = 跑完了，可能有部分实体失败）</summary>
        public bool Success { get; set; }
        /// <summary>全盘致命错误（整个迁移中断）</summary>
        public string Error { get; set; }
        /// <summary>差异计算异常</summary>
        public string DiffError { get; set; }
        /// <summary>部分实体 DDL 失败列表（不中断迁移，其他实体正常处理）</summary>
        public List<string> FailedEntities { get; set; } = new();

        public bool HasChanges => NewTables.Count > 0 || NewColumns.Count > 0;
        public bool HasFailures => FailedEntities.Count > 0;
    }

    public class TableColumnChange
    {
        public string TableName { get; set; }
        public HashSet<string> Columns { get; set; } = new();
    }

    public class DbSchemaSnapshot
    {
        public Dictionary<string, TableInfo> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Error { get; set; }
    }

    public class TableInfo
    {
        public string Name { get; set; }
        public HashSet<string> Columns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    #endregion
}
