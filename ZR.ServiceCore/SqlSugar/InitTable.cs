using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlSugar.IOC;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 初始化表（已重构：自动发现实体 → 差异报告 → 迁移历史 → 单实体容错）
    /// </summary>
    public class InitTable
    {
        /// <summary>
        /// 自动创建db、表、补充缺失列。实体由 DbMigrationService 显式注册表管理。
        /// </summary>
        public static void InitDb(bool init, IWebHostEnvironment env)
        {
            if (!init) return;

            var db = DbScoped.SugarScope;

            // 核心：显式注册表实体 → 差异检测 → CodeFirst 迁移（逐个容错）→ 报告输出 + 历史记录
            var report = DbMigrationService.Migrate(db, env);

            if (report.Success)
            {
                // 确保默认租户存在
                EnsureDefaultTenant(db);

                // 调度各业务模块的非SaaS初始化（如商城、内容等）
                if (InternalApp.ServiceProvider != null)
                {
                    using var scope = InternalApp.ServiceProvider.CreateScope();
                    var moduleInitializers = scope.ServiceProvider.GetServices<ITenantModuleInitializer>();
                    foreach (var mi in moduleInitializers)
                    {
                        mi.InitializeNonSaaS();
                    }
                }
            }
        }

        /// <summary>
        /// 多租户存量库迁移：为 sys_user_msg / sys_file / sys_file_group / sys_tasks / sys_tasks_log
        /// 补加 TenantId 列。幂等，所有环境执行。
        /// </summary>
        public static void MigrateTenantColumns()
        {
            var mainDb = DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);

            var tables = new[] { "sys_user_msg", "sys_file", "sys_file_group", "sys_tasks", "sys_tasks_log" };
            foreach (var table in tables)
            {
                AddTenantIdColumnIfMissing(mainDb, table);
            }
        }

        private static void AddTenantIdColumnIfMissing(ISqlSugarClient db, string tableName)
        {
            try
            {
                if (!db.DbMaintenance.IsAnyTable(tableName, false)) return;
                if (db.DbMaintenance.IsAnyColumn(tableName, "TenantId")) return;

                var dataType = db.CurrentConnectionConfig.DbType switch
                {
                    DbType.MySql => "varchar(64)",
                    DbType.SqlServer => "varchar(64)",
                    DbType.PostgreSQL => "varchar(64)",
                    DbType.Oracle => "VARCHAR2(64)",
                    _ => "varchar(64)"
                };

                db.DbMaintenance.AddColumn(tableName, new DbColumnInfo
                {
                    TableName = tableName,
                    DbColumnName = "TenantId",
                    DataType = dataType,
                    IsNullable = true
                });

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[迁移] 已为存量表 {tableName} 添加列 TenantId {dataType} NULL");
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[迁移] 为表 {tableName} 添加 TenantId 列失败: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        /// <summary>
        /// 开发环境初始化入口
        /// </summary>
        public static void RunInitDb(IWebHostEnvironment environment)
        {
            if (!environment.IsDevelopment()) return;

            var options = App.OptionsSetting;
            if (!options.InitDb) return;

            bool confirmed;
            if (!Console.IsInputRedirected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("检测到 InitDb=true，将执行数据库初始化（自动迁移 + 种子数据）。");
                Console.WriteLine("按 [回车] 确认执行，按其他键取消：");

                var input = ReadLineWithTimeout(TimeSpan.FromSeconds(30))?.Trim();
                confirmed = string.IsNullOrEmpty(input);
            }
            else
            {
                Console.WriteLine("非交互环境，直接执行数据库初始化。");
                confirmed = true;
            }

            if (!confirmed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("已取消初始化。如需正常启动请将 InitDb 改为 false。");
                Console.ResetColor();
                Environment.Exit(1);
            }

            try
            {
                InitDb(options.InitDb, environment);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"数据库迁移失败：{ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Environment.Exit(1);
            }

            try
            {
                InitSeedData(environment);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"初始化种子数据失败：{ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                Environment.Exit(1);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==== 数据库初始化完成 ====");
            Console.ResetColor();
        }

        private static string ReadLineWithTimeout(TimeSpan timeout)
        {
            var task = Task.Run(() => Console.ReadLine());
            return task.Wait(timeout) ? task.Result : null;
        }

        public static void InitSeedData(IWebHostEnvironment environment)
        {
            var path = Path.Combine(environment.WebRootPath, "data.xlsx");
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[种子数据] 未找到 {path}，跳过自动初始化");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            try
            {
                SeedDataService seedDataService = new();
                var result = seedDataService.InitSeedData(path, false);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("==== 种子数据初始化完成 ====");
                foreach (var item in result)
                    Console.WriteLine(item);
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[种子数据] 自动初始化失败，事务已回滚: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void EnsureDefaultTenant(SqlSugarScope db)
        {
            var mainDb = App.MainDbConfigId;
            var hasMainTenant = db.Queryable<SysTenant>().Any(x => x.DelFlag == 0 && x.TenantId == mainDb);
            if (hasMainTenant) return;

            db.Insertable(new SysTenant
            {
                TenantId = mainDb,
                TenantName = "默认租户",
                Status = 0,
                DelFlag = 0,
                Remark = "系统初始化自动创建"
            }).ExecuteCommand();
        }
    }
}
