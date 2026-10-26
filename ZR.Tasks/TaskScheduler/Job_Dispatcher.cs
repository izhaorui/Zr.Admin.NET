using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Cache;
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
using ZR.Infrastructure.Helper;
using ZR.Model.System;
using ZR.Model.System.Enums;
using ZR.Model.System.Tenant;
using ZR.ServiceCore.Services;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 统一任务分发器：Quartz 只调度本任务，运行时按租户展开执行。
    /// </summary>
    /// <remarks>
    /// [DisallowConcurrentExecution]：同一 JobKey（即同一任务）在上一次执行未结束时，
    /// 新的触发不会并发进入，而是被 Quartz 阻塞、待当前执行完成后按 Misfire 策略处理。
    /// 与下方按「任务+触发时刻」的去重键配合：
    /// 去重键负责多实例（多节点）同一触发时刻只跑一次，本特性负责单实例内「上次没跑完不再叠加」。
    /// 注意：跨节点仍各由自己的调度器执行，本特性不跨节点生效。
    /// </remarks>
    [DisallowConcurrentExecution]
    [AppService(ServiceType = typeof(Job_Dispatcher), ServiceLifetime = LifeTime.Scoped)]
    public class Job_Dispatcher : IJob
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 防重占位锁的存活时长（分钟）。键按「任务+触发时刻」生成，一次触发用完即不再复用，
        /// 仅需覆盖单次执行耗时以避免多实例并发重复执行。
        /// </summary>
        private const int DispatchDedupMinutes = 30;
        private const int MaxExceptionLength = 5000;

        public async Task Execute(IJobExecutionContext context)
        {
            var taskId = GetDataString(context, "TaskId") ?? context.JobDetail.Key.Name;
            var taskService = (ISysTasksQzService)App.GetRequiredService(typeof(ISysTasksQzService));
            var logService = (ISysTasksLogService)App.GetRequiredService(typeof(ISysTasksLogService));

            var template = await taskService.GetByIdAsync(taskId);
            if (template == null)
            {
                // 模板被删除后，Quartz 中的 Job 与 Trigger 成为孤儿：每次触发都空转，
                // 既不写日志也不会自行消失。此处清理调度项并落一条失败日志，避免长期占用调度资源。
                logger.Warn("JobDispatcher 未找到任务模板，清理孤儿调度: {TaskId}", taskId);

                try
                {
                    await context.Scheduler.DeleteJob(context.JobDetail.Key);
                }
                catch (Exception ex)
                {
                    // 删除失败不影响本次收尾：下一轮触发会再次进入本分支重试
                    logger.Error(ex, "JobDispatcher 清理孤儿调度失败: {TaskId}", taskId);
                }

                // 模板已不存在，无法回填 JobName/JobGroup（表中为 NOT NULL），
                // 用触发时的 JobKey 兜底，保留可排查信息。
                await logService.AddTaskLog(taskId, new SysTasksLog
                {
                    Status = "1",
                    JobName = context.JobDetail.Key.Name,
                    JobGroup = context.JobDetail.Key.Group,
                    JobMessage = "任务模板不存在，已从调度器移除",
                    Elapsed = 0,
                    Operator = GetDataString(context, "UserName") ?? "system",
                    TraceId = GetDataString(context, "TraceId") ?? Activity.Current?.TraceId.ToString(),
                    IsManual = context.MergedJobDataMap.ContainsKey("IsManual") ? Convert.ToInt32(context.MergedJobDataMap["IsManual"]) : 0,
                    TriggerSource = GetDataString(context, "TriggerSource"),
                    InvokeTarget = context.JobDetail.JobType.FullName
                });

                return;
            }

            var tenants = ResolveTargetTenantIds(template.TenantId);
            if (tenants.Count == 0)
            {
                logger.Warn("JobDispatcher 未解析到目标租户: task={TaskId}, tenantExpr={Expr}", taskId, template.TenantId);
                return;
            }

            // 多实例防重：同一任务、同一次计划触发只允许一个实例执行。
            // 依赖 ICache.TrySet —— 启用 Redis 时为跨节点原子占位，未启用时退化为单实例内存占位。
            var fireTime = (context.ScheduledFireTimeUtc ?? context.FireTimeUtc).UtcDateTime;
            var dedupKey = $"zr:task:dispatch:{taskId}:{fireTime:yyyyMMddHHmmss}";
            if (!CacheStore.Default.TrySet(dedupKey, 1, DispatchDedupMinutes))
            {
                logger.Debug("JobDispatcher 检测到本次触发已被其他实例接管，跳过: task={TaskId}, fireTime={FireTime}", taskId, fireTime);
                return;
            }

            // 标记本次为调度器分发模式，供 JobBase 识别并跳过日志/统计。
            // Quartz 的 MergedJobDataMap 是触发时生成的快照，只改 JobDetail.JobDataMap 任务内部读不到，需同时写入。
            context.MergedJobDataMap["DispatcherMode"] = true;
            context.JobDetail.JobDataMap["DispatcherMode"] = true;

            var overallStatus = "0";
            string overallMsg = null;
            var now = DateTime.Now;
            var invokeTarget = ResolveInvokeTarget(template);

            foreach (var tenantId in tenants)
            {
                using var tenantScope = TenantContext.Change(tenantId);
                var sw = Stopwatch.StartNew();
                var status = "0";
                var msg = "success";
                Exception jobException = null;

                try
                {
                    await ExecuteSingleTenant(template, context, tenantId);
                }
                catch (Exception ex)
                {
                    status = "1";
                    msg = ex.Message;
                    jobException = ex;
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
                    Exception = FormatException(jobException),
                    Elapsed = sw.Elapsed.TotalMilliseconds,
                    TenantId = tenantId,
                    Operator = GetDataString(context, "UserName"),
                    TraceId = GetDataString(context, "TraceId") ?? Activity.Current?.TraceId.ToString(),
                    IsManual = context.MergedJobDataMap.ContainsKey("IsManual") ? Convert.ToInt32(context.MergedJobDataMap["IsManual"]) : 0,
                    TriggerSource = GetDataString(context, "TriggerSource"),
                    InvokeTarget = invokeTarget
                }, template);
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

        /// <summary>
        /// 计算日志展示用的执行目标。HTTP/SQL 任务由分发器内联执行，
        /// 不再依赖数据库里可能为空或过期的 AssemblyName/ClassName。
        /// </summary>
        private static string ResolveInvokeTarget(SysTasks task)
        {
            return task.TaskType switch
            {
                2 => "HttpRequest",
                3 => "SqlExecute",
                _ => $"{task.AssemblyName}.{task.ClassName}"
            };
        }

        private static string FormatException(Exception exception)
        {
            if (exception == null)
            {
                return null;
            }

            var text = exception.ToString();
            return text.Length <= MaxExceptionLength ? text : text[..MaxExceptionLength];
        }

        /// <summary>
        /// 安全读取 JobDataMap 字符串：Quartz 的 GetString 在键不存在时会抛 KeyNotFoundException。
        /// </summary>
        private static string GetDataString(IJobExecutionContext context, string key)
        {
            var data = context.MergedJobDataMap;
            return data.ContainsKey(key) ? data.GetString(key) : null;
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
                    .Where(x => x.DelFlag == DelFlagEnum.Normal && x.Status == 0)
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
                ExecuteSqlTask(task, tenantId);
                return;
            }

            await ExecuteAssemblyTask(task, context, tenantId);
        }

        private static async Task ExecuteHttpTask(SysTasks info)
        {
            ValidateHttpTarget(info.ApiUrl);

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

        /// <summary>
        /// 校验 HTTP 任务目标地址：只允许 http/https，阻止 file://、gopher:// 等协议
        /// 被用来读取服务器本地文件或发起非预期请求。
        /// </summary>
        /// <remarks>
        /// 内网/回环地址不做拦截 —— 定时任务调用内网接口是常见用法，硬编码拦截会直接打断存量任务。
        /// 若部署环境需要防 SSRF（如任务配置可能被低权限用户篡改），应在网络层限制出站目标，
        /// 而不是在应用内按地址段猜测。
        /// </remarks>
        private static void ValidateHttpTarget(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new CustomException("HTTP 任务未配置请求地址");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new CustomException($"HTTP 任务地址仅支持 http/https 协议：{url}");
            }
        }

        private static void ExecuteSqlTask(SysTasks info, string tenantId)
        {
            if (info.SqlText.IsEmpty())
            {
                throw new CustomException($"任务{info.ID}执行失败，SQL为空");
            }

            if (!SqlTaskGuard.IsSafe(info.SqlText, out var unsafeReason))
            {
                throw new CustomException($"任务{info.ID} SQL 未通过安全校验，已拒绝执行：{unsafeReason}");
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException($"任务{info.ID}执行失败，租户标识为空");
            }

            var result = DbScoped.SugarScope.GetConnectionScope(tenantId).Ado.ExecuteCommand(info.SqlText);
            logger.Info("任务【{TaskName}】SQL执行结果={Result}, tenant={TenantId}", info.Name, result, tenantId);
        }

        private static async Task ExecuteAssemblyTask(SysTasks info, IJobExecutionContext context, string tenantId)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(info.AssemblyName));
            Type jobType = assembly.GetType(info.AssemblyName + "." + info.ClassName)
                ?? throw new CustomException($"未找到任务类型: {info.AssemblyName}.{info.ClassName}");

            var jobObj = App.GetService(jobType) ?? Activator.CreateInstance(jobType)
                ?? throw new CustomException($"无法实例化任务类型: {jobType.FullName}");

            // 兼容现有 JobBase/自定义 Job 逻辑：把本次租户写入上下文 Map。
            // Quartz 的 MergedJobDataMap 是触发时快照，仅写 JobDetail.JobDataMap 时任务内部读取会抛
            // KeyNotFoundException: Key TenantId not found，因此必须直接写入 MergedJobDataMap。
            context.MergedJobDataMap["TenantId"] = tenantId;
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
