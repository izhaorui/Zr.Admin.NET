using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using NLog;
using Quartz;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ZR.Model.System;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.Services;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 统一任务分发器：Quartz 只调度本任务，运行时按租户展开执行。
    /// </summary>
    [AppService(ServiceType = typeof(Job_Dispatcher), ServiceLifetime = LifeTime.Scoped)]
    public class Job_Dispatcher : IJob
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public async Task Execute(IJobExecutionContext context)
        {
            var taskId = context.MergedJobDataMap.GetString("TaskId") ?? context.JobDetail.Key.Name;
            var taskService = (ISysTasksQzService)App.GetRequiredService(typeof(ISysTasksQzService));
            var logService = (ISysTasksLogService)App.GetRequiredService(typeof(ISysTasksLogService));

            var template = await taskService.GetByIdAsync(taskId);
            if (template == null)
            {
                logger.Warn("JobDispatcher 未找到任务模板: {TaskId}", taskId);
                return;
            }

            var tenants = ResolveTargetTenantIds(template.TenantId);
            if (tenants.Count == 0)
            {
                logger.Warn("JobDispatcher 未解析到目标租户: task={TaskId}, tenantExpr={Expr}", taskId, template.TenantId);
                return;
            }

            // 标记本次为调度器分发模式，供 JobBase 识别并跳过日志/统计
            context.JobDetail.JobDataMap["DispatcherMode"] = true;

            var overallStatus = "0";
            string overallMsg = null;
            var now = DateTime.Now;

            foreach (var tenantId in tenants)
            {
                using var tenantScope = TenantContext.Change(tenantId);
                var sw = Stopwatch.StartNew();
                var status = "0";
                var msg = "success";

                try
                {
                    await ExecuteSingleTenant(template, context, tenantId);
                }
                catch (Exception ex)
                {
                    status = "1";
                    msg = ex.Message;
                    overallStatus = "1";
                    overallMsg = msg;
                    logger.Error(ex, "任务分发执行失败: task={TaskId}, tenant={TenantId}", taskId, tenantId);
                }
                finally
                {
                    sw.Stop();
                }

                await logService.AddTaskLog(taskId, new SysTasksLog
                {
                    Status = status,
                    JobMessage = msg,
                    Elapsed = sw.Elapsed.TotalMilliseconds,
                    TenantId = tenantId,
                    Operator = context.MergedJobDataMap.GetString("UserName"),
                    TraceId = context.MergedJobDataMap.GetString("TraceId") ?? Activity.Current?.TraceId.ToString(),
                    IsManual = context.MergedJobDataMap.ContainsKey("IsManual") ? Convert.ToInt32(context.MergedJobDataMap["IsManual"]) : 0,
                    TriggerSource = context.MergedJobDataMap.GetString("TriggerSource"),
                    InvokeTarget = $"{template.AssemblyName}.{template.ClassName}"
                });
            }

            // 所有租户执行完毕后，统一更新一次任务统计
            await taskService.UpdateAsync(f => new SysTasks
            {
                RunTimes = f.RunTimes + 1,
                LastRunTime = now,
                LastRunStatus = overallStatus,
                LastErrorMsg = overallStatus == "1" ? overallMsg : null,
                LastFailTime = overallStatus == "1" ? now : f.LastFailTime,
                LastSuccessTime = overallStatus == "0" ? now : f.LastSuccessTime
            }, f => f.ID == taskId);
        }

        private static List<string> ResolveTargetTenantIds(string tenantExpression)
        {
            var expr = (tenantExpression ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(expr))
            {
                return [];
            }

            if (expr == "*")
            {
                return DbScoped.SugarScope.Queryable<SysTenant>()
                    .Where(x => x.DelFlag == 0 && x.Status == 0)
                    .Select(x => x.TenantId)
                    .ToList();
            }

            return expr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static async Task ExecuteSingleTenant(SysTasks task, IJobExecutionContext context, string tenantId)
        {
            if (task.TaskType == 2)
            {
                await ExecuteHttpTask(task);
                return;
            }

            if (task.TaskType == 3)
            {
                ExecuteSqlTask(task);
                return;
            }

            await ExecuteAssemblyTask(task, context, tenantId);
        }

        private static async Task ExecuteHttpTask(SysTasks info)
        {
            string result;
            if (info.RequestMethod != null && info.RequestMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                result = await HttpHelper.HttpPostAsync(info.ApiUrl, info.JobParams);
            }
            else
            {
                var url = info.ApiUrl;
                if (url.IndexOf("?") > -1)
                {
                    url += "&" + info.JobParams;
                }
                else
                {
                    url += "?" + info.JobParams;
                }
                result = await HttpHelper.HttpGetAsync(url);
            }

            logger.Info("任务【{TaskName}】网络请求执行结果={Result}", info.Name, result);
        }

        private static void ExecuteSqlTask(SysTasks info)
        {
            if (info.SqlText.IsEmpty())
            {
                throw new CustomException($"任务{info.ID}执行失败，SQL为空");
            }

            var result = DbScoped.SugarScope.Ado.ExecuteCommandWithGo(info.SqlText);
            logger.Info("任务【{TaskName}】SQL执行结果={Result}", info.Name, result);
        }

        private static async Task ExecuteAssemblyTask(SysTasks info, IJobExecutionContext context, string tenantId)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(info.AssemblyName));
            Type jobType = assembly.GetType(info.AssemblyName + "." + info.ClassName)
                ?? throw new CustomException($"未找到任务类型: {info.AssemblyName}.{info.ClassName}");

            var jobObj = App.GetService(jobType) ?? Activator.CreateInstance(jobType)
                ?? throw new CustomException($"无法实例化任务类型: {jobType.FullName}");

            // 兼容现有 JobBase 逻辑：把本次租户写入上下文 Map。
            context.JobDetail.JobDataMap["TenantId"] = tenantId;

            if (jobObj is IJob quartzJob)
            {
                await quartzJob.Execute(context);
                return;
            }

            var runMethod = jobType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (runMethod != null)
            {
                var invokeResult = runMethod.Invoke(jobObj, null);
                if (invokeResult is Task t)
                {
                    await t;
                }
                return;
            }

            throw new CustomException($"任务类型不支持执行: {jobType.FullName}");
        }
    }
}
