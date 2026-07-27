using Microsoft.AspNetCore.Mvc;
using SqlSugar.IOC;
using ZR.Mall.Service;
using ZR.ServiceCore.Services;

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
            var mallDb = DbScoped.SugarScope.GetConnectionScope(App.MallDbConfigId);
            var mallDiff = MallTenantInitializer.ComputeMallDiff(mallDb);

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
                var mallDb = DbScoped.SugarScope.GetConnectionScope(App.MallDbConfigId);
                foreach (var entityType in MallTenantInitializer.MallEntityTypes)
                {
                    MallTenantInitializer.EnsureEntitySchema(mallDb, entityType);
                }
                var mallDiff = MallTenantInitializer.ComputeMallDiff(mallDb);
                logs.Add($"商城库：同步完成，剩余缺失表 {mallDiff.NewTables.Count} 张，缺失列 {mallDiff.NewColumns.Sum(c => c.Columns.Count)} 个");
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
                    var seedDataService = new SeedDataService();
                    var seedResult = seedDataService.InitMallMenuSeedData();
                    logs.AddRange(seedResult);
                }
                catch (Exception ex)
                {
                    logs.Add($"种子数据同步异常: {ex.Message}");
                }
            }

            return SUCCESS(new { Logs = logs, Success = true });
        }
    }
}
