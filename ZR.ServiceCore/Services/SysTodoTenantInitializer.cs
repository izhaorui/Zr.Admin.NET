using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.Extensions.Hosting;
using SqlSugar;
using SqlSugar.IOC;
using ZR.Model.System;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 个人待办模块租户级表初始化器。
    /// 新租户创建时自动在租户库建 sys_todo 表；非 SaaS 开发模式下在主库建表。
    /// </summary>
    [AppService(ServiceType = typeof(ITenantModuleInitializer))]
    public class SysTodoTenantInitializer : ITenantModuleInitializer
    {
        public string ModuleName => "Todo";

        public string InitializeTenant(string tenantId)
        {
            if (!App.IsTenantEnabled())
            {
                return "多租户未启用，跳过个人待办表初始化";
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException("租户标识不能为空", nameof(tenantId));
            }

            var db = DbScoped.SugarScope.GetConnectionScope(tenantId);
            db.CodeFirst.InitTables(typeof(SysTodo));
            EnsureRemindedColumn(db);

            return $"个人待办表初始化完成（{tenantId}）";
        }

        public void InitializeNonSaaS()
        {
            if (!App.OptionsSetting.InitDb) return;
            if (!InternalApp.WebHostEnvironment.IsDevelopment()) return;

            var db = DbScoped.SugarScope.GetConnectionScope(App.MainDbConfigId);
            db.CodeFirst.InitTables(typeof(SysTodo));
            EnsureRemindedColumn(db);
        }

        /// <summary>
        /// 为已存在的 sys_todo 表补充 reminded 列（SqlSugar InitTables 不会修改已存在表结构）
        /// </summary>
        private static void EnsureRemindedColumn(ISqlSugarClient db)
        {
            if (!db.DbMaintenance.IsAnyColumn("sys_todo", "reminded"))
            {
                db.DbMaintenance.AddColumn("sys_todo", new DbColumnInfo
                {
                    TableName = "sys_todo",
                    DbColumnName = "reminded",
                    DataType = "int",
                    Length = 11,
                    IsNullable = true,
                    DefaultValue = "0"
                });
            }
        }
    }
}
