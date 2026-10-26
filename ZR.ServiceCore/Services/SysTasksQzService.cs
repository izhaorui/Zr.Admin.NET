using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 定时任务
    /// </summary>
    [AppService(ServiceType = typeof(ISysTasksQzService), ServiceLifetime = LifeTime.Transient)]
    public class SysTasksQzService : BaseService<SysTasks>, ISysTasksQzService
    {
        /// <summary>
        /// 查询任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SysTasks> SelectTaskList(TasksQueryDto parm)
        {
            var predicate = Expressionable.Create<SysTasks>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.QueryText),
                m => m.Name.Contains(parm.QueryText) ||
                m.JobGroup.Contains(parm.QueryText) ||
                m.AssemblyName.Contains(parm.QueryText));
            predicate.AndIF(parm.TaskType != null, m => m.TaskType == parm.TaskType);
            predicate.AndIF(parm.IsStart != null, m => m.IsStart == parm.IsStart);

            return Queryable().Where(predicate.ToExpression())
                .ToPage(parm);
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int AddTasks(SysTasks parm)
        {
            parm.IsStart = 0;

            return Add(parm);
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int UpdateTasks(SysTasks parm)
        {
            return Update(f => f.ID == parm.ID, f => new SysTasks
            {
                ID = parm.ID,
                Name = parm.Name,
                JobGroup = parm.JobGroup,
                Cron = parm.Cron,
                AssemblyName = parm.AssemblyName,
                ClassName = parm.ClassName,
                Remark = parm.Remark,
                TriggerType = parm.TriggerType,
                IntervalSecond = parm.IntervalSecond,
                JobParams = parm.JobParams,
                Update_time = DateTime.Now,
                BeginTime = parm.BeginTime,
                EndTime = parm.EndTime,
                TaskType = parm.TaskType,
                ApiUrl = parm.ApiUrl,
                SqlText = parm.SqlText,
                RequestMethod = parm.RequestMethod,
                TenantId = parm.TenantId,
                // 白名单更新：漏加这一项会导致前端改了「错过执行策略」但保存不生效
                MisfirePolicy = parm.MisfirePolicy,
            });
        }

        /// <summary>
        /// 规范化任务的租户表达式（新建/修改任务时使用）。
        /// 规则集中在服务层，避免新增入口（批量启停、复制任务等）时复制一份。
        /// </summary>
        /// <param name="tenantExpression">前端提交的租户表达式</param>
        public string NormalizeTenantExpression(string tenantExpression, ISysTenantService tenantService)
        {
            var currentTenantId = App.GetCurrentTenantId();

            // 非多租户：没有租户隔离，沿用提交值（为空则用当前租户）
            if (!App.IsTenantEnabled())
            {
                return string.IsNullOrWhiteSpace(tenantExpression) ? currentTenantId : tenantExpression.Trim();
            }

            // 非主租户：只能操作自身，提交值一律忽略
            if (!App.IsMainTenant())
            {
                return currentTenantId;
            }

            if (string.IsNullOrWhiteSpace(tenantExpression))
            {
                return currentTenantId;
            }

            var expr = tenantExpression.Trim();
            if (expr == "*")
            {
                return expr;
            }

            var parts = expr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parts.Count == 0)
            {
                return currentTenantId;
            }

            foreach (var tenantId in parts)
            {
                tenantService.CheckTenant(tenantId);
            }

            return string.Join(",", parts);
        }
    }
}

