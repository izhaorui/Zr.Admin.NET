using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlSugar.IOC;
using ZR.Model;
using ZR.Model.Content;
using ZR.Model.Models;
using ZR.Model.Public;
using ZR.Model.System;
using ZR.Model.System.Generate;
using ZR.Model.System.Model;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 初始化表
    /// </summary>
    public class InitTable
    {
        /// <summary>
        /// 创建db、表
        /// </summary>
        public static void InitDb(bool init)
        {
            var db = DbScoped.SugarScope;
            //可在此处单独更新某个表的结构，无视配置
            //例如：db.CodeFirst.InitTables(typeof(EmailLog));
            

            if (!init) return;
            StaticConfig.CodeFirst_MySqlCollate = "utf8mb4_general_ci";
            //建库：如果不存在创建数据库存在不会重复创建 
            db.DbMaintenance.CreateDatabase();// 注意 ：Oracle和个别国产库需不支持该方法，需要手动建库 

            db.CodeFirst.InitTables(typeof(SysUser));
            db.CodeFirst.InitTables(typeof(SysRole));
            db.CodeFirst.InitTables(typeof(SysDept));
            db.CodeFirst.InitTables(typeof(SysPost));
            db.CodeFirst.InitTables(typeof(SysFile));
            db.CodeFirst.InitTables(typeof(SysConfig));
            db.CodeFirst.InitTables(typeof(SysNotice));
            db.CodeFirst.InitTables(typeof(SysLogininfor));
            db.CodeFirst.InitTables(typeof(SysOperLog));
            db.CodeFirst.InitTables(typeof(SysMenu));
            db.CodeFirst.InitTables(typeof(SysRoleMenu));
            db.CodeFirst.InitTables(typeof(SysRoleDept));
            db.CodeFirst.InitTables(typeof(SysUserRole));
            db.CodeFirst.InitTables(typeof(SysUserPost));
            db.CodeFirst.InitTables(typeof(SysTasks));
            db.CodeFirst.InitTables(typeof(SysTasksLog));
            db.CodeFirst.InitTables(typeof(SysTenant));
            db.CodeFirst.InitTables(typeof(SysTenantPlan));
            db.CodeFirst.InitTables(typeof(SysTenantPlanBinding));
            db.CodeFirst.InitTables(typeof(SysTenantPlanMenu));
            db.CodeFirst.InitTables(typeof(CommonLang));
            db.CodeFirst.InitTables(typeof(GenTable));
            db.CodeFirst.InitTables(typeof(GenTableColumn));
            db.CodeFirst.InitTables(typeof(SysDictData));
            db.CodeFirst.InitTables(typeof(SysDictType));
            db.CodeFirst.InitTables(typeof(SqlDiffLog));
            db.CodeFirst.InitTables(typeof(EmailTpl));
            db.CodeFirst.InitTables(typeof(SmsCodeLog));
            db.CodeFirst.InitTables(typeof(EmailLog));
            db.CodeFirst.InitTables(typeof(Article));
            db.CodeFirst.InitTables(typeof(ArticleCategory));
            db.CodeFirst.InitTables(typeof(ArticleBrowsingLog));
            db.CodeFirst.InitTables(typeof(ArticlePraise));
            db.CodeFirst.InitTables(typeof(ArticleComment));
            db.CodeFirst.InitTables(typeof(ArticleTopic));
            db.CodeFirst.InitTables(typeof(BannerConfig));
            db.CodeFirst.InitTables(typeof(SysUserMsg));
            db.CodeFirst.InitTables(typeof(SysFileGroup));
            db.CodeFirst.InitTables(typeof(SysTodo));
            EnsureDefaultTenant(db);

            // 调度各业务模块的非SaaS初始化（如商城、内容等）
            // 模块自行判断 InitDb/IsDevelopment 条件，无需在此重复检查
            // ITenantModuleInitializer 默认按 Scoped 注册，必须从子作用域解析（根 Provider 不允许解析 Scoped 服务）
            if (InternalApp.ServiceProvider != null)
            {
                using var scope = InternalApp.ServiceProvider.CreateScope();
                var moduleInitializers = scope.ServiceProvider.GetServices<ITenantModuleInitializer>();
                foreach (var mi in moduleInitializers)
                {
                    mi.InitializeNonSaaS();
                }
            }

            //db.CodeFirst.InitTables(typeof(SocialFans));
            //db.CodeFirst.InitTables(typeof(SocialFansInfo));
            //db.CodeFirst.InitTables(typeof(UserOnlineLog));
        }
        public static void InitNewTb()
        {
            var db = DbScoped.SugarScope;
            var t1 = db.DbMaintenance.IsAnyTable(typeof(UserOnlineLog).Name);
            if (!t1)
            {
                db.CodeFirst.InitTables(typeof(UserOnlineLog));
            }

            var t2 = db.DbMaintenance.IsAnyTable("sys_tenant");
            if (!t2)
            {
                db.CodeFirst.InitTables(typeof(SysTenant));
            }

            var t3 = db.DbMaintenance.IsAnyTable("sys_tenant_plan");
            if (!t3)
            {
                db.CodeFirst.InitTables(typeof(SysTenantPlan));
            }

            var t4 = db.DbMaintenance.IsAnyTable("sys_tenant_plan_binding");
            if (!t4)
            {
                db.CodeFirst.InitTables(typeof(SysTenantPlanBinding));
            }
            var t5 = db.DbMaintenance.IsAnyTable("sys_tenant_plan_menu");
            if (!t5)
            {
                db.CodeFirst.InitTables(typeof(SysTenantPlanMenu));
            }
            EnsureDefaultTenant(db);
        }

        private static void EnsureDefaultTenant(SqlSugarScope db)
        {
            var mainDb = App.MainDbConfigId;
            var hasMainTenant = db.Queryable<SysTenant>().Any(x => x.DelFlag == 0 && x.TenantId == mainDb);
            if (hasMainTenant)
            {
                return;
            }

            db.Insertable(new SysTenant
            {
                TenantId = mainDb,
                TenantName = "默认租户",
                Status = 0,
                DelFlag = 0,
                Remark = "系统初始化自动创建"
            }).ExecuteCommand();
        }

        /// <summary>
        /// 多租户存量库迁移：为 sys_user_msg / sys_file / sys_file_group 补加 TenantId 隔离列（varchar(64) NULL）。
        /// 新装库由 CodeFirst 自动建列；存量库若缺此列，主库连接上的租户过滤器会在运行时报“未知列”错误。
        /// 幂等：列已存在或表不存在则跳过。在所有环境（含生产）启动时执行，无需手动干预。
        /// </summary>
        public static void MigrateTenantColumns()
        {
            var mainDb = DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);
            var tables = new[] { "sys_user_msg", "sys_file", "sys_file_group" };
            foreach (var table in tables)
            {
                AddTenantIdColumnIfMissing(mainDb, table);
            }
        }

        private static void AddTenantIdColumnIfMissing(ISqlSugarClient db, string tableName)
        {
            try
            {
                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                {
                    return; // 表不存在（未启用相关模块），跳过
                }
                if (db.DbMaintenance.IsAnyColumn(tableName, "TenantId"))
                {
                    return; // 列已存在，跳过
                }

                // 按数据库类型映射列类型，保持与实体 [SugarColumn(Length = 64, IsNullable = true)] 一致
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
        /// 开发环境初始化入口：InitDb=true 时提示用户确认后自动执行建表 + 种子数据。
        /// 由 SqlsugarSetup.AddDb 调用，整合了交互确认、建表、种子导入的完整流程。
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
                Console.WriteLine("检测到 InitDb=true，将执行数据库初始化（建表 + 种子数据）。");
                Console.WriteLine("按 [回车] 确认执行，按其他键取消：");
                //Console.Out.Flush();
                //Console.ResetColor();

                var input = ReadLineWithTimeout(TimeSpan.FromSeconds(30))?.Trim();
                confirmed = string.IsNullOrEmpty(input);
            }
            else
            {
                Console.WriteLine("非交互环境，直接执行数据库初始化（建表 + 种子数据）。");
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
                InitDb(options.InitDb);
                InitNewTb();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"初始化表（建表）失败：{ex.Message}");
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
            Console.WriteLine("==== 数据库初始化完成（建表:是, 种子:是） ====");
            Console.ResetColor();
        }

        private static string ReadLineWithTimeout(TimeSpan timeout)
        {
            var task = Task.Run(() => Console.ReadLine());
            return task.Wait(timeout) ? task.Result : null;
        }

        /// <summary>
        /// 自动导入种子数据（data.xlsx）。在建表完成后由启动流程调用，免去手动调用接口
        /// </summary>
        public static void InitSeedData(IWebHostEnvironment environment)
        {
            var path = Path.Combine(environment.WebRootPath, "data.xlsx");
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[种子数据] 未找到 {path}，跳过自动初始化（如需初始化请放置 data.xlsx 到 wwwroot）");
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
                {
                    Console.WriteLine(item);
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[种子数据] 自动初始化失败，事务已回滚: {ex.Message}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
}
