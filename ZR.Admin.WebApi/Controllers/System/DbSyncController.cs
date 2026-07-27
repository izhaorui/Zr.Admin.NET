using Microsoft.AspNetCore.Mvc;
using Infrastructure;
using SqlSugar.IOC;
using ZR.Model;
using ZR.Model.System;
using ZR.ServiceCore.SqlSugar;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 数据库结构同步（前端可视化：预览差异 / 一键同步表结构）。
    /// 仅负责主库（MainDb）；商城库同步见 ZR.Mall 的 shopping/dbSync。
    /// </summary>
    [Route("system/dbSync")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class DbSyncController : BaseController
    {
        /// <summary>
        /// 预览当前数据库与实体模型的结构差异（主库），不执行任何 DDL。
        /// </summary>
        [HttpGet("diff")]
        [ActionPermissionFilter(Permission = "system:dbSync:diff")]
        public IActionResult Diff()
        {
            var mainDb = DbScoped.SugarScope;
            var mainDiff = DbMigrationService.Diff(mainDb);

            var result = new
            {
                MainDb = new
                {
                    NewTables = mainDiff.NewTables,
                    NewColumns = mainDiff.NewColumns.Select(c => new { c.TableName, Columns = c.Columns.ToList() }).ToList(),
                    HasChanges = mainDiff.HasChanges
                },
                HasAnyChange = mainDiff.HasChanges
            };

            return SUCCESS(result);
        }

        /// <summary>
        /// 执行同步：补齐缺失的表与列（主库）。
        /// 幂等、安全：仅 ADD 缺失对象，不删除列，不修改列类型。
        /// </summary>
        [HttpPost("sync")]
        [ActionPermissionFilter(Permission = "system:dbSync:sync")]
        public IActionResult Sync()
        {
            var logs = new List<string>();

            // 主库结构同步（建表 + 补列）
            try
            {
                var mainDb = DbScoped.SugarScope;
                var mainReport = DbMigrationService.Migrate(mainDb, App.WebHostEnvironment);
                logs.Add($"主库：新增表 {mainReport.NewTables.Count} 张，新增列 {mainReport.NewColumns.Sum(c => c.Columns.Count)} 个" +
                         (mainReport.HasFailures ? $"，失败 {mainReport.FailedEntities.Count} 个实体" : ""));
                foreach (var t in mainReport.NewTables) logs.Add($"  + 表 {t}");
                foreach (var tc in mainReport.NewColumns)
                    foreach (var col in tc.Columns)
                        logs.Add($"  + 列 {tc.TableName}.{col}");
                foreach (var err in mainReport.FailedEntities) logs.Add($"  ! {err}");
            }
            catch (Exception ex)
            {
                logs.Add($"主库同步异常: {ex.Message}");
            }

            return SUCCESS(new { Logs = logs, Success = true });
        }
    }
}
