using Infrastructure;
using SqlSugar.IOC;
using System.Reflection;
using ZR.Model;
using ZR.Model.AI;
using ZR.Model.Content;
using ZR.Model.Models;
using ZR.Model.Public;
using ZR.Model.social;
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
            typeof(SysTenantOrder),
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
            typeof(SysUser2faDevice),
            typeof(SysUserWebAuthnCredential),
            typeof(SysFileGroup),
            typeof(DailySchedule),
            typeof(UserOnlineLog),
            typeof(AiChatMessage),
            typeof(AiChatSession),
            typeof(AiCallLog),
            typeof(AiAccessPolicy),
            typeof(AiModelPrice),
            // AI 运行时配置覆盖（后台维护的模型/密钥/知识库配置）
            typeof(AiConfig),
            // AI 模型渠道（后台维护的供应商渠道行）
            typeof(AiProvider),
            // 知识库检索审计（可选插件写）：表不存在时插件只记日志不报错，
            // 但管理端要看命中率就必须建表，因此纳入主库迁移清单。
            typeof(AiKnowledgeQueryLog),
            // 行政区划字典（主库）：公共参考数据，运费模板的「不配送地区/包邮」按它匹配。
            // 注册进主库迁移链路后，后台「数据库同步」页也能看到并手工补建这张表。
            // 数据由 MainDbReferenceSeed.EnsureAreas 灌入（种子内嵌在 ZR.ServiceCore）。
            typeof(Area),
            // 第三方账号绑定。数据无 IMainDbEntity，SaaS 下由 BaseRepository 路由到租户库，
            // 因此这里（系统库/单库）与 TenantBusinessEntityTypes（租户库）都要注册。
            typeof(SysThirdAccount),
            // SSO 应用接入（主库平台级配置）
            typeof(SysSsoApp),
        };

        /// <summary>
        /// 租户库业务表（无 IMainDbEntity，SaaS 下由 BaseRepository 路由到当前租户库）。
        /// 新建租户 InitializeTenant 与启动 EnsureTenantBusinessTables 共用此清单（建表+补列）。
        /// 不含商城/工作流（由各自 ITenantModuleInitializer 建）。
        /// </summary>
        public static readonly Type[] TenantBusinessEntityTypes =
        {
            typeof(SysUser),
            typeof(SysRole),
            typeof(SysDept),
            typeof(SysPost),
            typeof(SysNotice),
            typeof(SysLogininfor),
            typeof(SysOperLog),
            typeof(SysRoleMenu),
            typeof(SysRoleDept),
            typeof(SysUserRole),
            typeof(SysUserPost),
            typeof(SysTenantDictData),
            typeof(SysUser2faDevice),
            typeof(SysUserWebAuthnCredential),
            typeof(UserOnlineLog),
            typeof(SqlDiffLog),
            typeof(SmsCodeLog),
            typeof(Article),
            typeof(ArticleCategory),
            typeof(ArticlePraise),
            typeof(ArticleComment),
            typeof(ArticleTopic),
            typeof(ArticleUserCircles),
            typeof(SocialFans),
            typeof(SocialFansInfo),
            typeof(DailySchedule),
            typeof(ArticleBrowsingLog),
            typeof(AiChatSession),
            typeof(AiChatMessage),
            typeof(SysThirdAccount)
        };

        /// <summary>
        /// 启动补 TenantId 列的排除清单。
        /// 这些实体虽实现 IMainDbEntity 且有 TenantId 属性，但不参与每次启动的存量补列：
        /// 属可选业务模块的表，结构由 --initdb / 手工同步维护，避免核心启动链路耦合可选模块。
        /// 仅作用于 <see cref="MigrateTenantColumns"/>，不影响 <see cref="Migrate"/> 全量迁移（仍会建表/补列）。
        /// </summary>
        private static readonly HashSet<Type> StartupTenantColumnExcludedTypes = new()
        {
            typeof(AiCallLog),
            typeof(AiAccessPolicy),
            // SaaS 平台级表：TenantId 是业务主字段（租户标识 / 被绑定的租户），不是"租户隔离列"，
            // 不需要每次启动做补列检查。结构由 --initdb / SaaS 模块初始化维护。
            typeof(SysTenant),
            typeof(SysTenantPlanBinding),
            typeof(SysTenantOrder),
        };

        /// <summary>
        /// 历史表名 → 当前实体表名 的重命名映射。
        ///
        /// 用途：实体 <c>[SugarTable]</c> 改名后，存量库不会自动改名 —— 迁移只会「新建空表」，
        /// 旧表及其数据被留在库里。迁移前先按此映射把旧表改名，数据/主键/索引一并保留。
        ///
        /// 约定：key = 当前实体表名（新名），value = 历史表名（旧名）。
        /// 新增改名时在这里加一行即可，建表、--initdb、后台一键同步、SaaS 启动补表都会自动生效。
        /// </summary>
        private static readonly Dictionary<string, string> LegacyTableRenames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["sys_user_online_log"] = "userOnlineLog",
                ["sys_sql_diff_log"] = "sqlDiffLog",
                ["article_category"] = "articleCategory",
            };

        /// <summary>
        /// 应用全部历史表名重命名（遍历映射，主库）。供 Web 启动的幂等迁移入口调用。
        /// </summary>
        public static void ApplyLegacyTableRenames()
        {
            ApplyLegacyTableRenames(DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId));
        }

        /// <summary>
        /// 应用全部历史表名重命名（遍历映射，指定库）。幂等、失败不阻断。
        /// </summary>
        public static void ApplyLegacyTableRenames(ISqlSugarClient db)
        {
            if (db == null) return;

            foreach (var newTableName in LegacyTableRenames.Keys)
            {
                TryRenameLegacyTable(db, newTableName);
            }
        }

        /// <summary>
        /// 单个实体的历史表名重命名：仅当「新表不存在 &amp;&amp; 旧表存在」时执行，天然幂等。
        /// 用 SqlSugar 的 <c>DbMaintenance.RenameTable</c>，由它按当前 DbType 生成方言 DDL
        /// （SQL Server sp_rename / MySQL RENAME TABLE / Oracle、PgSql、Sqlite 各自语法），无需手写 SQL。
        /// 失败只告警，不阻断该实体的建表与补列。
        /// </summary>
        private static void TryRenameLegacyTable(ISqlSugarClient db, string newTableName)
        {
            if (string.IsNullOrEmpty(newTableName)) return;
            if (!LegacyTableRenames.TryGetValue(newTableName, out var oldTableName)) return;
            if (string.Equals(oldTableName, newTableName, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                if (db.DbMaintenance.IsAnyTable(newTableName, false)) return;   // 已是新表名，无需处理
                if (!db.DbMaintenance.IsAnyTable(oldTableName, false)) return;  // 旧表不存在（新装库或已迁移过）

                var renamed = db.DbMaintenance.RenameTable(oldTableName, newTableName);
                Log.WriteLine(renamed ? ConsoleColor.Cyan : ConsoleColor.Yellow,
                    renamed
                        ? $"[DbMigration] 表重命名完成：{oldTableName} → {newTableName}"
                        : $"[DbMigration] 表重命名未生效：{oldTableName} → {newTableName}");
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] 表重命名失败（{oldTableName} → {newTableName}）：{ex.Message}");
            }
        }

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
        /// 系统注册表 + 配置 AdditionalTypes - [SkipMigration]。
        /// </summary>
        private static List<Type> ResolveSystemEntities(out List<string> skipped)
        {
            return ResolveEntityTypes(SystemEntityTypes, App.OptionsSetting.DbMigration?.AdditionalTypes, out skipped);
        }

        /// <summary>
        /// 针对单个具体连接（如商城 MallDb 库）计算表/列快照，供差异预览使用。
        /// </summary>
        public static DbSchemaSnapshot GetDbSchemaForConnection(ISqlSugarClient db)
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
                    catch (Exception ex)
                    {
                        // 列信息不可用时把该表整体排除出快照：若保留空的列集合，下游（ComputeEntityVsDbDiff /
                        // ComputeDiff）会把该表判定为"零列"，把一次读取故障放大成一整屏假的"新增列"。
                        // 排除后下游最多把它当作"表不存在"给出一条可解释的提示，代价小得多。
                        Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] 读取表 {table.Name} 的列信息失败，已跳过该表: {ex.Message}");
                        continue;
                    }
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
        public static MigrationReport Migrate(SqlSugarScope db)
        {
            var report = new MigrationReport();
            var options = App.OptionsSetting;
            var reportOnly = options.DbMigration?.ReportOnly ?? false;
            var additionalTypes = options.DbMigration?.AdditionalTypes;

            // 1) 确保迁移历史表存在（自举）
            EnsureMigrationHistoryTable(db);

            // 2) 解析实体列表（系统注册表 + 配置文件扩展 - [SkipMigration] 排除）
            var entities = ResolveSystemEntities(out var skipped);

            Log.WriteLine(ConsoleColor.Cyan, $"[DbMigration] 注册实体 {SystemEntityTypes.Length} 个，实际迁移 {entities.Count} 个");
            if (additionalTypes is { Length: > 0 })
            {
                Log.WriteLine(ConsoleColor.Cyan, $"[DbMigration] 配置文件额外类型: {string.Join(", ", additionalTypes)}");
            }
            foreach (var s in skipped)
            {
                Log.WriteLine(ConsoleColor.DarkGray, $"[DbMigration]   [跳过] {s}");
            }

            // 3) 迁移前快照
            var beforeSchema = GetDbSchemaForConnection(db);

            // 4) ReportOnly 模式：复用 Diff 逻辑，仅对比实体模型与数据库实际结构，不执行 DDL
            if (reportOnly)
            {
                report = Diff(db);
                PrintReport(report);
                return report;
            }

            // 5) 执行 CodeFirst 迁移（逐个实体，单个失败不影响其他）
            var batchId = $"{DateTime.Now:yyyyMMddHHmmss}_{Random.Shared.Next(1000, 9999)}";
            var migrationErrors = new List<string>();

            StaticConfig.CodeFirst_MySqlCollate = "utf8mb4_general_ci";

            // 建库（如不存在）
            db.DbMaintenance.CreateDatabase();

            // CodeFirst.InitTables 对已存在的表不会 ALTER 加列，故用 EnsureEntitySchema
            // 同时处理"建新表"与"已有表补缺失列"两种场景（幂等，不删列/不改类型）。
            // 单列补列失败不会抛出，故用回调把它一并汇入 migrationErrors，
            // 否则这类失败只在控制台留一行黄字，报告/迁移历史/前端同步日志里都看不到。
            EnsureEntitySchemas(db, entities, msg => migrationErrors.Add(msg));

            report.Success = migrationErrors.Count == 0;
            if (migrationErrors.Count > 0)
            {
                report.FailedEntities = migrationErrors;
            }

            // 6) 迁移后快照 & 计算差异
            var afterSchema = GetDbSchemaForConnection(db);
            ComputeDiff(beforeSchema, afterSchema, report);

            // 7) 记录迁移历史
            SaveMigrationHistory(db, batchId, report);

            // 8) 打印报告
            PrintReport(report);

            return report;
        }

        /// <summary>
        /// 批量确保实体表结构。单个实体失败不中断后续实体。
        /// <paramref name="onError"/> 收到的是 "实体名: 明细"；不传则实体级异常只打黄字日志。
        /// </summary>
        public static void EnsureEntitySchemas(ISqlSugarClient db, IEnumerable<Type> types, Action<string> onError = null)
        {
            foreach (var entityType in types)
            {
                try
                {
                    EnsureEntitySchema(db, entityType, msg => onError?.Invoke($"{entityType.Name}: {msg}"));
                }
                catch (Exception ex)
                {
                    var detail = $"{entityType.Name}: {ex.Message}";
                    if (onError != null)
                        onError(detail);
                    else
                        Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] {detail}");
                }
            }
        }

        /// <summary>
        /// 确保单个实体对应的表结构：表不存在则建表；表已存在则补齐实体模型中新增的列。
        /// 幂等 —— 不会删除列，也不会修改已有列的类型/长度（避免破坏存量数据）。
        /// 兼容 SQL Server / MySQL：列类型优先取 SqlSugar 按当前 DbType 推导的权威类型，
        /// 兜底按 .NET 属性类型映射。NOT NULL 判定与 SqlsugarSetup.EntityService 约定一致。
        /// <paramref name="onColumnError"/> 非空时，单列补列失败会额外回调上报（用于汇入迁移报告）；
        /// 不传则行为与历史一致：只打印告警、不中断其他表/列。
        /// </summary>
        public static void EnsureEntitySchema(ISqlSugarClient db, Type entityType, Action<string> onColumnError = null)
        {
            var entityInfo = db.EntityMaintenance.GetEntityInfo(entityType);
            var tableName = entityInfo.DbTableName;

            // 历史表名重命名：改名后的实体在存量库需先把旧表改名，否则下面会按新名建出空表、旧表及数据残留
            TryRenameLegacyTable(db, tableName);

            // 表不存在：直接建整套表（含所有列）
            if (!db.DbMaintenance.IsAnyTable(tableName, false))
            {
                db.CodeFirst.InitTables(entityType);
                return;
            }

            // 表已存在：拉取现有列，逐列补齐模型中存在但库中缺失的列
            var existingColumns = db.DbMaintenance.GetColumnInfosByTableName(tableName, false)
                .Select(c => c.DbColumnName.ToLowerInvariant())
                .ToHashSet();

            foreach (var col in entityInfo.Columns)
            {
                if (col.IsIgnore) continue;
                if (existingColumns.Contains(col.DbColumnName.ToLowerInvariant())) continue;

                var prop = entityType.GetProperty(col.PropertyName);
                var sugarAttr = prop?.GetCustomAttribute<SugarColumn>();
                var isNotNull = sugarAttr?.ExtendedAttribute?.ToString() == ProteryConstant.NOTNULL.ToString();

                try
                {
                    // SqlSugar 对可空列无 DefaultValue 特性时会填充字符串 "NULL"，
                    // 直接传给 AddColumn 会生成非法 SQL（"... DEFAULT NULL"），归一为空以跳过 DEFAULT 子句。
                    var defaultValue = NormalizeDefaultValue(col.DefaultValue);

                    var added = db.DbMaintenance.AddColumn(tableName, new DbColumnInfo
                    {
                        DbColumnName = col.DbColumnName,
                        DataType = ResolveColumnDataType(db, col, prop),
                        IsNullable = !isNotNull,
                        DefaultValue = defaultValue
                    });

                    if (added)
                    {
                        Log.WriteLine(ConsoleColor.Green, $"[DbMigration] 表 {tableName} 已补列 {col.DbColumnName} {(isNotNull ? "NOT NULL" : "NULL")}");
                    }
                    else
                    {
                        // 未抛异常但返回未生效：同样要让调用方可见，避免静默留下一张缺列的表
                        var detail = $"表 {tableName} 补列 {col.DbColumnName} 未生效（AddColumn 返回 false）";
                        Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] {detail}");
                        onColumnError?.Invoke(detail);
                    }
                }
                catch (Exception ex)
                {
                    // 单个列补列失败（如 NOT NULL 且无默认值、存量数据冲突）不中断其他表/列，
                    // 打印提示交由人工处理，保证迁移整体可用；同时回调上报，使其进入迁移报告与历史。
                    var detail = $"表 {tableName} 补列 {col.DbColumnName} 失败: {ex.Message}";
                    Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] {detail}");
                    onColumnError?.Invoke(detail);
                }
            }
        }

        /// <summary>
        /// 解析某实体列对应的数据库列类型。优先使用 SqlSugar 按当前 DbType 推导的权威类型
        /// （col.DataType 对 SQL Server 已是正确的 varchar(n)/bigint/datetime 等）；
        /// 当该值为空（常见于 MySQL 未显式标注 ColumnDataType 的值类型）时，按 .NET 属性类型兜底映射。
        /// </summary>
        public static string ResolveColumnDataType(ISqlSugarClient db, EntityColumnInfo col, PropertyInfo prop)
        {
            if (!string.IsNullOrWhiteSpace(col.DataType)
                && !col.DataType.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                // CodeFirst_BigString 等"按数据库选类型"的写法，在 EntityColumnInfo.DataType 中
                // 表现为逗号分隔的多类型串（如 "varcharmax,longtext,text,clob"）。SqlSugar 建表时
                // 会按当前 DbType 选其中一段，但本项目自定义 AddColumn 不会自动裁剪，原样拼进
                // ALTER 会生成非法 SQL（"..." ADD [X] varcharmax,longtext,text,clob NULL"）。
                // 这里显式按当前数据库挑出对应的长文本类型。
                // CodeFirst_BigString 是无括号的多库类型串（varcharmax,longtext,text,clob）。
                // decimal(18,6) / numeric(18,2) 也含逗号，必须按真实 SQL 类型原样返回。
                if (col.DataType.Contains(',') && !col.DataType.Contains('('))
                {
                    return ResolveBigStringType(db);
                }
                return col.DataType;
            }

            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (type == typeof(string))
            {
                var len = col.Length > 0 ? col.Length : 255;
                return len > 2000 ? "text" : $"varchar({len})";
            }
            if (type == typeof(long)) return "bigint";
            if (type == typeof(int)) return "int";
            if (type == typeof(short) || type == typeof(byte)) return "smallint";
            if (type == typeof(decimal))
                return col.DecimalDigits > 0 ? $"decimal(18,{col.DecimalDigits})" : "decimal(18,2)";
            if (type == typeof(double) || type == typeof(float)) return "double";
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "datetime";
            if (type == typeof(bool) || type.IsEnum) return "int";
            if (type == typeof(Guid)) return "char(36)";
            return "varchar(255)";
        }

        /// <summary>
        /// 把 CodeFirst_BigString 这种"逗号分隔多数据库类型串"解析为当前数据库对应的长文本类型。
        /// 多用于补列场景（自定义 AddColumn 不会像 CodeFirst 那样自动按 DbType 裁剪类型串）。
        /// </summary>
        private static string ResolveBigStringType(ISqlSugarClient db)
        {
            return db.CurrentConnectionConfig.DbType switch
            {
                DbType.SqlServer => "varchar(max)",
                DbType.MySql => "longtext",
                DbType.Sqlite => "clob",
                DbType.PostgreSQL => "text",
                DbType.Oracle => "clob",
                _ => "text"
            };
        }

        /// <summary>
        /// 仅计算主库结构差异（不执行任何 DDL），供前端"同步结构"页面预览。
        /// 等价于 ReportOnly 模式，但返回 MigrationReport 对象而非打印到控制台。
        /// </summary>
        public static MigrationReport Diff(SqlSugarScope db)
        {
            return DiffEntities(db, ResolveSystemEntities(out _));
        }

        /// <summary>
        /// 将指定实体模型与数据库实际结构对比（不执行 DDL），供主库/商城等差异预览。
        /// 表名、列名一律取自 SqlSugar 的权威映射（GetEntityInfo）。
        /// </summary>
        public static MigrationReport DiffEntities(ISqlSugarClient db, IReadOnlyList<Type> entityTypes)
        {
            var report = new MigrationReport();
            var dbSchema = GetDbSchemaForConnection(db);
            ComputeEntityVsDbDiff(db, entityTypes, dbSchema, report);
            report.Success = true;
            return report;
        }

        /// <summary>
        /// 多租户存量库补列：对显式注册表中实现 IMainDbEntity 且含 TenantId 属性的实体，
        /// 幂等补加 TenantId 列。所有环境启动时执行（新装库由 CodeFirst 建列，此处只兜底存量库）。
        /// 实体来源与 Migrate 一致：SystemEntityTypes + 配置 AdditionalTypes - [SkipMigration]，
        /// 再减去 StartupTenantColumnExcludedTypes（可选模块表不在启动补列范围内）。
        /// </summary>
        public static void MigrateTenantColumns()
        {
            var mainDb = DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);
            var entities = ResolveSystemEntities(out _);

            var addedColumns = new List<string>();
            foreach (var tableName in entities
                .Where(t => typeof(IMainDbEntity).IsAssignableFrom(t) && t.GetProperty("TenantId") != null)
                .Where(t => !StartupTenantColumnExcludedTypes.Contains(t))
                .Select(t => t.GetCustomAttribute<SugarTable>()?.TableName)
                .Where(name => name != null))
            {
                if (AddTenantIdColumnIfMissing(mainDb, tableName))
                {
                    addedColumns.Add(tableName);
                }
            }

            // 有实际补列时记录到迁移历史（历史表不存在则静默跳过，不影响主流程）
            if (addedColumns.Count > 0)
            {
                SaveTenantColumnHistory(mainDb, addedColumns);
            }
        }

        /// <summary>
        /// 存量租户库补齐业务表：无 IMainDbEntity 的表运行时走租户库，但历史 InitializeTenant 可能没建。
        /// 跳过主库；单库失败不阻断其他租户。幂等。
        /// </summary>
        public static void EnsureTenantBusinessTables()
        {
            if (!App.IsTenantEnabled()) return;

            var configs = App.OptionsSetting?.DbConfigs;
            if (configs == null || configs.Count == 0) return;

            var mainId = App.MainDbConfigId;
            foreach (var cfg in configs)
            {
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.ConfigId)) continue;
                if (string.Equals(cfg.ConfigId, mainId, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var db = DbScoped.SugarScope.GetConnectionScope(cfg.ConfigId);
                    EnsureEntitySchemas(db, TenantBusinessEntityTypes);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(ConsoleColor.Yellow,
                        $"[DbMigration] 租户库[{cfg.ConfigId}]补齐业务表失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 幂等补加 TenantId 列，返回是否实际执行了 DDL
        /// </summary>
        private static bool AddTenantIdColumnIfMissing(ISqlSugarClient db, string tableName)
        {
            try
            {
                if (!db.DbMaintenance.IsAnyTable(tableName, false)) return false;
                if (db.DbMaintenance.IsAnyColumn(tableName, "TenantId")) return false;

                var dataType = db.CurrentConnectionConfig.DbType == DbType.Oracle ? "VARCHAR2(64)" : "varchar(64)";

                db.DbMaintenance.AddColumn(tableName, new DbColumnInfo
                {
                    DbColumnName = "TenantId",
                    DataType = dataType,
                    IsNullable = true
                });

                Log.WriteLine(ConsoleColor.Green, $"[DbMigration] 已为存量表 {tableName} 添加列 TenantId {dataType} NULL");
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[DbMigration] 为表 {tableName} 添加 TenantId 列失败: {ex.Message}");
                return false;
            }
        }

        private static void SaveTenantColumnHistory(ISqlSugarClient db, List<string> tables)
        {
            TryInsertHistory(db, new DbMigrationHistory
            {
                BatchId = $"{DateTime.Now:yyyyMMddHHmmss}_tenantcol",
                Summary = $"存量库补 TenantId 列 {tables.Count} 张表",
                Details = System.Text.Json.JsonSerializer.Serialize(new { TenantIdColumnAdded = tables }),
                AppliedAt = DateTime.Now,
                NewTables = 0,
                NewColumns = tables.Count,
                Success = true
            }, requireTable: true, failMessage: "记录 TenantId 补列历史失败");
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
                Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] 创建迁移历史表失败（不影响主流程）: {ex.Message}");
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
        /// ReportOnly 模式：将实体模型与数据库实际结构对比，预测变更。
        /// 列名一律取自 SqlSugar 的权威映射（GetEntityInfo），避免手写 ToSnakeCase 与
        /// 实际建表列名（属性原名）不一致导致误报大量"新增列"。
        /// </summary>
        private static void ComputeEntityVsDbDiff(ISqlSugarClient db, IEnumerable<Type> entityTypes, DbSchemaSnapshot dbSchema, MigrationReport report)
        {
            foreach (var entityType in entityTypes)
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

                    var colName = col.DbColumnName;
                    if (existingTable.Columns.Contains(colName.ToLowerInvariant())) continue;

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

        private static void SaveMigrationHistory(SqlSugarScope db, string batchId, MigrationReport report)
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

            TryInsertHistory(db, new DbMigrationHistory
            {
                BatchId = batchId,
                Summary = summary,
                Details = details,
                AppliedAt = DateTime.Now,
                NewTables = report.NewTables.Count,
                NewColumns = totalNewCols,
                Success = report.Success,
                Error = Truncate(report.Error, 3900)
            }, requireTable: false, failMessage: "记录迁移历史失败");
        }

        private static void TryInsertHistory(ISqlSugarClient db, DbMigrationHistory history, bool requireTable, string failMessage)
        {
            try
            {
                if (requireTable && !db.DbMaintenance.IsAnyTable("__db_migration_history", false)) return;
                db.Insertable(history).ExecuteCommand();
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Yellow, $"[DbMigration] {failMessage}: {ex.Message}");
            }
        }

        private static string NormalizeDefaultValue(string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(defaultValue)
                || defaultValue.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return defaultValue;
        }

        private static void PrintReport(MigrationReport report)
        {
            Log.WriteLine(ConsoleColor.White, "");
            Log.WriteLine(ConsoleColor.Cyan, "========== 数据库迁移报告 ==========");

            // 全局致命错误：report.Error 仅在整体流程无法继续时赋值，实体级部分失败不会赋值。
            // 这里刻意不用 !report.Success 短路 —— Success=false 与"FailedEntities 非空"（部分实体失败）等价，
            // 那样会把下面的 NewTables / NewColumns / FailedEntities 明细一起吞掉，只剩一行空的"迁移中断"。
            if (report.Error != null)
            {
                Log.WriteLine(ConsoleColor.Red, $"迁移中断: {report.Error}");
                return;
            }

            // 无任何变更也无错误
            if (!report.HasChanges && !report.HasFailures)
            {
                Log.WriteLine(ConsoleColor.Green, "数据库结构与实体模型一致，无需变更。");
                Log.WriteLine(ConsoleColor.White, "====================================");
                return;
            }

            // 差异变更
            if (report.NewTables.Count > 0)
            {
                Log.WriteLine(ConsoleColor.Green, $"--- 新增表 ({report.NewTables.Count}) ---");
                foreach (var t in report.NewTables)
                    Log.WriteLine(ConsoleColor.White, $"  + {t}");
            }

            if (report.NewColumns.Count > 0)
            {
                Log.WriteLine(ConsoleColor.Yellow, $"--- 新增列 ({report.NewColumns.Sum(c => c.Columns.Count)}) ---");
                foreach (var tc in report.NewColumns)
                {
                    Log.WriteLine(ConsoleColor.White, $"  [{tc.TableName}]");
                    foreach (var col in tc.Columns)
                        Log.WriteLine(ConsoleColor.White, $"    + {col}");
                }
            }

            // 部分实体失败
            if (report.HasFailures)
            {
                Log.WriteLine(ConsoleColor.Red, $"--- DDL 失败 ({report.FailedEntities.Count}) ---");
                foreach (var err in report.FailedEntities)
                    Log.WriteLine(ConsoleColor.White, $"  ! {err}");
            }

            Log.WriteLine(report.HasFailures ? ConsoleColor.Yellow : ConsoleColor.Green,
                $"迁移{(report.HasFailures ? "部分成功" : "成功")}: 新增 {report.NewTables.Count} 表, {report.NewColumns.Sum(c => c.Columns.Count)} 列"
                + (report.HasFailures ? $", 失败 {report.FailedEntities.Count} 个实体" : ""));
            Log.WriteLine(ConsoleColor.White, "====================================");

            if (report.DiffError != null)
            {
                Log.WriteLine(ConsoleColor.Yellow, $"注意: 差异计算异常 ({report.DiffError})，以上报告可能不完整");
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
