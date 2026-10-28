using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using Infrastructure.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Quartz;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
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

        private sealed class TaskExecutionTimeoutException : TimeoutException
        {
            public Task ExecutionTask { get; }

            public TaskExecutionTimeoutException(string message, Exception innerException = null, Task executionTask = null)
                : base(message, innerException)
            {
                ExecutionTask = executionTask;
            }
        }

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

            // 全局执行策略（QuartzTask 节点）。默认 0/0/false，与改造前行为一致。
            var taskOptions = App.Configuration.GetSection("QuartzTask");
            var timeoutSeconds = Math.Max(0, taskOptions.GetValue("TimeoutSeconds", 0));
            var maxRetryCount = Math.Max(0, taskOptions.GetValue("MaxRetryCount", 0));
            var retryIntervalSeconds = Math.Max(0, taskOptions.GetValue("RetryIntervalSeconds", 10));

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
            context.MergedJobDataMap["DispatcherMode"] = true;

            var overallStatus = "0";
            string overallMsg = null;
            var now = DateTime.Now;
            var invokeTarget = ResolveInvokeTarget(template);

            foreach (var tenantId in tenants)
            {
                using var tenantScope = TenantContext.Change(tenantId);

                // 每个租户建一个作用域：任务类内部通过 App.GetRequiredService 解析的服务，
                // 依赖链可能含 scoped（如 IEmailSendService / IHubContext），根容器无法解析。
                // AsyncLocal 保证并发任务各自独立，且不影响请求线程（请求优先走 HttpContext.RequestServices）。
                var serviceScope = InternalApp.ServiceProvider?.CreateScope();
                var previousProvider = InternalApp.ScopedServiceProvider.Value;
                if (serviceScope != null)
                {
                    InternalApp.ScopedServiceProvider.Value = serviceScope.ServiceProvider;
                }

                var deferScopeDisposal = false;
                var stopAfterTenant = false;
                try
                {
                    var sw = Stopwatch.StartNew();
                    var status = "0";
                    var msg = "success";
                    Exception jobException = null;
                    var retried = 0;

                    for (var attempt = 0; ; attempt++)
                    {
                        try
                        {
                            await ExecuteTenantWithTimeout(template, context, tenantId, timeoutSeconds);
                            break;
                        }
                        catch (Exception ex)
                        {
                            var retryable = ex is not TimeoutException && ex is not OperationCanceledException;
                            if (retryable && attempt < maxRetryCount)
                            {
                                retried++;
                                logger.Warn("任务分发执行失败，准备重试: task={TaskId}, tenant={TenantId}, attempt={Attempt}/{Max}, error={Error}",
                                    taskId, tenantId, attempt + 1, maxRetryCount, ex.Message);

                                if (retryIntervalSeconds > 0)
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(retryIntervalSeconds));
                                }
                                continue;
                            }

                            status = "1";
                            // 重试过程中每次的异常都记录在 Warn，这里只保留最终失败原因与重试次数
                            var failureMessage = ex is OperationCanceledException
                                ? $"任务执行已取消：{ex.Message}"
                                : ex.Message;
                            msg = retried > 0 ? $"{failureMessage}（已重试 {retried} 次）" : failureMessage;
                            jobException = ex;
                            overallStatus = "1";
                            overallMsg = msg;

                            if (ex is TaskExecutionTimeoutException { ExecutionTask: not null } timeoutException)
                            {
                                var executionStillRunning = !timeoutException.ExecutionTask.IsCompleted;
                                stopAfterTenant = executionStillRunning;
                                deferScopeDisposal = executionStillRunning;
                                ObserveTimedOutExecution(
                                    timeoutException.ExecutionTask,
                                    taskId,
                                    tenantId,
                                    deferScopeDisposal ? serviceScope : null);
                                if (executionStillRunning)
                                {
                                    logger.Warn($"任务超时后执行结果未知，已发送取消信号；停止后续租户执行: task={taskId}, tenant={tenantId}");
                                }
                            }

                            logger.Error(ex, "任务分发执行失败: task={TaskId}, tenant={TenantId}, retried={Retried}",
                                taskId, tenantId, retried);
                            break;
                        }
                    }

                    sw.Stop();

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

                    // 通知放在日志之后：先保证失败结果可查，再对外发消息
                    if (jobException != null)
                    {
                        JobFailureNotifier.Notify(taskId, template.Name, tenantId, msg, jobException);
                    }
                    else
                    {
                        // 恢复后清除冷却，避免下一次新故障被上一次的冷却窗口吞掉
                        JobFailureNotifier.ResetCooldown(taskId, tenantId);
                    }
                }
                finally
                {
                    InternalApp.ScopedServiceProvider.Value = previousProvider;
                    if (!deferScopeDisposal)
                    {
                        serviceScope?.Dispose();
                    }
                }

                if (stopAfterTenant)
                {
                    break;
                }
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
                3 => $"StoredProcedure:{task.SqlText}",
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

        /// <summary>
        /// 执行单个租户的任务，超时后向任务发送取消信号。
        /// SQL/未实现协作取消的程序集任务仍可能继续运行，因此超时结果视为未知且不可自动重试。
        /// </summary>
        private static async Task ExecuteTenantWithTimeout(SysTasks task, IJobExecutionContext context, string tenantId, int timeoutSeconds)
        {
            using var executionCts = new CancellationTokenSource();
            var execTask = timeoutSeconds > 0
                ? Task.Run(() => ExecuteSingleTenant(task, context, tenantId, timeoutSeconds, executionCts.Token))
                : ExecuteSingleTenant(task, context, tenantId, timeoutSeconds, executionCts.Token);

            if (timeoutSeconds <= 0)
            {
                await execTask;
                return;
            }

            var completedTask = await Task.WhenAny(execTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds + 5)));
            if (completedTask == execTask)
            {
                await execTask;
                return;
            }

            try
            {
                executionCts.Cancel();
            }
            catch (AggregateException ex)
            {
                logger.Error(ex, $"任务超时后取消回调异常: task={task.ID}, tenant={tenantId}");
            }

            throw new TaskExecutionTimeoutException(
                $"任务执行超时（外层等待>{timeoutSeconds + 5}s；配置值={timeoutSeconds}s），已发送取消信号，执行结果未知",
                executionTask: execTask);
        }

        private static void ObserveTimedOutExecution(Task executionTask, string taskId, string tenantId, IServiceScope serviceScope)
        {
            _ = executionTask.ContinueWith(completedTask =>
            {
                try
                {
                    if (completedTask.IsFaulted)
                    {
                        logger.Error(completedTask.Exception, $"超时任务在取消信号后仍以异常结束: task={taskId}, tenant={tenantId}");
                    }
                    else
                    {
                        logger.Warn($"超时任务已结束，但原执行结果仍按未知处理: task={taskId}, tenant={tenantId}, canceled={completedTask.IsCanceled}");
                    }
                }
                finally
                {
                    serviceScope?.Dispose();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, System.Threading.Tasks.TaskScheduler.Default);
        }

        private static async Task ExecuteSingleTenant(
            SysTasks task,
            IJobExecutionContext context,
            string tenantId,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (task.TaskType == 2)
            {
                await ExecuteHttpTask(task, timeoutSeconds, cancellationToken);
                return;
            }

            if (task.TaskType == 3)
            {
                ExecuteSqlTask(task, tenantId);
                return;
            }

            await ExecuteAssemblyTask(task, context, tenantId, cancellationToken);
        }

        private static async Task ExecuteHttpTask(SysTasks info, int timeoutSeconds, CancellationToken cancellationToken)
        {
            ValidateHttpTarget(info.ApiUrl);

            // 请求级超时：未配置全局超时时沿用 HttpHelper 默认的 30 秒
            var requestTimeout = timeoutSeconds > 0 ? timeoutSeconds : 30;

            string result;
            try
            {
                if (info.RequestMethod != null && info.RequestMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    result = await HttpHelper.HttpPostAsync(
                        info.ApiUrl, info.JobParams, null, requestTimeout, null, cancellationToken);
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
                    result = await HttpHelper.HttpGetAsync(url, null, requestTimeout, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                throw new TaskExecutionTimeoutException(
                    $"HTTP 请求超时（>{requestTimeout}s），执行结果未知：{info.ApiUrl}",
                    ex);
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
            if (StoredProcedureTaskExecutor.IsProcedureCall(info.SqlText))
            {
                var affectedRows = StoredProcedureTaskExecutor.Execute(info, tenantId);
                logger.Info("任务【{TaskName}】存储过程执行完成，过程={ProcedureName}, 影响行数={AffectedRows}, tenant={TenantId}",
                    info.Name, info.SqlText, affectedRows, tenantId);
                return;
            }

            if (info.SqlText.IsEmpty())
            {
                throw new CustomException($"任务{info.ID}执行失败，SQL语句为空");
            }

            if (!SqlTaskGuard.IsSafe(info.SqlText, out var unsafeReason))
            {
                throw new CustomException($"任务{info.ID} SQL 语句未通过安全校验：{unsafeReason}");
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new CustomException($"任务{info.ID}执行失败，租户标识为空");
            }

            var result = DbScoped.SugarScope.GetConnectionScope(tenantId).Ado.ExecuteCommand(info.SqlText);
            logger.Info("任务【{TaskName}】SQL执行结果={Result}, tenant={TenantId}", info.Name, result, tenantId);
        }

        /// <summary>
        /// 解析任务类型，兼容三种配置写法：
        /// 1) ClassName 已含完整命名空间（如 ZR.Mall.TaskScheduler.Job_X）；
        /// 2) 程序集名 + 类名（如 ZR.Mall.Job_X）；
        /// 3) 简类名 —— 内置任务统一放在 &lt;程序集&gt;.TaskScheduler 下，
        ///    而 MallModuleInitProvider / WorkflowModuleInitProvider 播种的 sys_tasks 只存了简类名，
        ///    只按「程序集名.类名」拼接会找不到类型，导致内置任务全部执行失败。
        /// </summary>
        private static Type ResolveJobType(Assembly assembly, SysTasks info)
        {
            var className = info.ClassName?.Trim();
            if (string.IsNullOrEmpty(className))
            {
                throw new CustomException($"任务{info.ID}未配置任务类名");
            }

            var type = assembly.GetType(className);
            if (type != null)
            {
                return type;
            }

            type = assembly.GetType($"{info.AssemblyName}.{className}");
            if (type != null)
            {
                return type;
            }

            type = assembly.GetType($"{info.AssemblyName}.TaskScheduler.{className}");
            if (type != null)
            {
                return type;
            }

            // 4) 兜底：按类名在程序集内查找。
            // 内置任务分散在不同命名空间（ZR.ServiceCore.Job、ZR.Mall.TaskScheduler 等），
            // 且部分任务类不实现 IJob、只提供 Run() 方法供反射调用，无法用接口过滤。
            try
            {
                var candidates = assembly.GetExportedTypes()
                    .Where(t => t.Name == className)
                    .ToList();

                // 同名时优先 IJob 实现（分发器能直接 Execute）
                return candidates.FirstOrDefault(t => typeof(IJob).IsAssignableFrom(t))
                    ?? candidates.FirstOrDefault();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "按类名扫描任务类型失败: {Assembly}.{ClassName}", info.AssemblyName, className);
                return null;
            }
        }

        private static async Task ExecuteAssemblyTask(
            SysTasks info,
            IJobExecutionContext context,
            string tenantId,
            CancellationToken cancellationToken)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(info.AssemblyName));
            Type jobType = ResolveJobType(assembly, info)
                ?? throw new CustomException($"未找到任务类型: {info.AssemblyName}.{info.ClassName}");

            var jobObj = App.GetService(jobType) ?? Activator.CreateInstance(jobType)
                ?? throw new CustomException($"无法实例化任务类型: {jobType.FullName}");

            // 兼容现有 JobBase/自定义 Job 逻辑：把本次租户与协作取消令牌写入上下文 Map。
            // Quartz 的 MergedJobDataMap 是触发时快照，仅写 JobDetail.JobDataMap 时任务内部读取会抛
            // KeyNotFoundException: Key TenantId not found，因此必须直接写入 MergedJobDataMap。
            context.MergedJobDataMap["TenantId"] = tenantId;

            if (jobObj is IJob quartzJob)
            {
                await quartzJob.Execute(context);
                return;
            }

            var runWithCancellation = jobType.GetMethod(
                "Run", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(CancellationToken) }, null);
            var runMethod = runWithCancellation ?? jobType.GetMethod(
                "Run", BindingFlags.Instance | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            if (runMethod != null)
            {
                var invokeResult = runWithCancellation != null
                    ? runMethod.Invoke(jobObj, new object[] { cancellationToken })
                    : runMethod.Invoke(jobObj, null);
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
