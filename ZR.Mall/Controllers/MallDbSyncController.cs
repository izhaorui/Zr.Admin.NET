using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using SqlSugar.IOC;
using ZR.Mall.Common;
using ZR.ServiceCore.Services;
using ZR.ServiceCore.SqlSugar;

namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 商城库结构同步（前端可视化：预览差异 / 一键同步表结构 + 商城菜单种子数据）。
    /// 与系统库同步（system/dbSync）分离，商城相关逻辑内聚在 ZR.Mall 模块内。
    /// </summary>
    [Route("shopping/dbSync")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class MallDbSyncController : BaseController
    {
        /// <summary>
        /// 预览商城库与实体模型的结构差异，不执行任何 DDL。
        /// </summary>
        [HttpGet("diff")]
        [ActionPermissionFilter(Permission = "system:dbSync:diff")]
        public IActionResult Diff()
        {
            var mallDb = ResolveMallSyncDb();
            var mallDiff = DbMigrationService.DiffEntities(mallDb, MallTenantInitializer.MallEntityTypes);

            var result = new
            {
                MallDb = new
                {
                    NewTables = mallDiff.NewTables,
                    NewColumns = mallDiff.NewColumns.Select(c => new { c.TableName, Columns = c.Columns.ToList() }).ToList(),
                    HasChanges = mallDiff.HasChanges
                }
            };

            return SUCCESS(result);
        }

        /// <summary>
        /// 执行商城库同步：补齐缺失的表与列，并刷新种子数据（商城菜单等）。
        /// 幂等、安全：仅 ADD 缺失对象，不删除列，不修改列类型。
        /// </summary>
        /// <param name="syncSeed">是否同步种子数据（商城菜单等），默认 true</param>
        [HttpPost("sync")]
        [ActionPermissionFilter(Permission = "system:dbSync:sync")]
        public IActionResult Sync([FromQuery] bool syncSeed = true)
        {
            var logs = new List<string>();

            // 1) 商城库结构同步（建表 + 补列）
            try
            {
                var mallDb = ResolveMallSyncDb();
                DbMigrationService.EnsureEntitySchemas(mallDb, MallTenantInitializer.MallEntityTypes);
                var mallDiff = DbMigrationService.DiffEntities(mallDb, MallTenantInitializer.MallEntityTypes);
                logs.Add($"商城库：同步完成，剩余缺失表 {mallDiff.NewTables.Count} 张，缺失列 {mallDiff.NewColumns.Sum(c => c.Columns.Count)} 个");

                // 商品名全文索引（幂等、失败只回显不阻断；未建时搜索自动回落 LIKE）
                logs.Add($"商品名全文索引：{ProductFullTextIndexEnsurer.Ensure(mallDb)}");
            }
            catch (Exception ex)
            {
                logs.Add($"商城库同步异常: {ex.Message}");
            }

            // 2) 种子数据（商城菜单等）
            if (syncSeed)
            {
                try
                {
                    // 商城菜单种子已内聚到 MallModuleInitProvider（随模块），不再经由框架层 SeedDataService 转发
                    var seedResult = new MallModuleInitProvider().SeedMenu();
                    logs.AddRange(seedResult);
                }
                catch (Exception ex)
                {
                    logs.Add($"种子数据同步异常: {ex.Message}");
                }
            }

            return SUCCESS(new { Logs = logs, Success = true });
        }

        /// <summary>
        /// 非 SaaS 同步共享商城库；SaaS 同步当前请求租户库。
        /// </summary>
        private static ISqlSugarClient ResolveMallSyncDb()
        {
            var configId = App.IsTenantEnabled() ? App.GetCurrentTenantId() : App.MallDbConfigId;
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new CustomException("无法解析商城库连接：租户标识为空");
            }

            return DbScoped.SugarScope.GetConnectionScope(configId);
        }
    }
}
