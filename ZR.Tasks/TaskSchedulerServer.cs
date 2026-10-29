using Infrastructure;
using Infrastructure.Model;
using NLog;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using ZR.Infrastructure.Helper;
using ZR.Model.System;
using ZR.Tasks.TaskScheduler;

namespace ZR.Tasks
{
    /// <summary>
    /// 计划任务中心
    /// </summary>
    //[AppService]
    public class TaskSchedulerServer : ITaskSchedulerServer
    {
        /// <summary>
        /// 调度器延迟初始化：构造阶段只登记创建方法，首次真正使用时才异步创建。
        /// Lazy 保证并发下只创建一次；不直接存 Task 是为了避免在 .Result 上同步阻塞，
        /// 并把初始化异常以原始异常而非 AggregateException 抛给调用方。
        /// </summary>
        private readonly Lazy<Task<IScheduler>> _scheduler;
        private readonly IJobFactory _jobFactory;
        /// <summary>
        /// 日志接口
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public TaskSchedulerServer(IJobFactory jobFactory)
        {
            _jobFactory = jobFactory;
            _scheduler = new Lazy<Task<IScheduler>>(CreateSchedulerAsync);
        }

        private static NameValueCollection GetQuartzConfig()
        {
            NameValueCollection collection = new NameValueCollection();
            var section = App.Configuration.GetSection("Quartz");
            foreach (var item in section.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    collection[item.Key] = item.Value;
                }
            }

            // 必须写 newtonsoft（Quartz.Serialization.Json）：Quartz 3.18 内核同时内置了
            // System.Text.Json 实现，配置成 "json" 会告警"该值将来会有歧义"。
            // binary 依赖的 BinaryFormatter 已从 .NET 9+ 运行时移除，net10 下启用持久化 JobStore 会抛 PlatformNotSupportedException。
            if (string.IsNullOrWhiteSpace(collection["quartz.serializer.type"]))
            {
                collection["quartz.serializer.type"] = "newtonsoft";
            }

            return collection;
        }

        /// <summary>
        /// 按 Quartz 配置创建调度器（由 Lazy 保证只执行一次）。
        /// </summary>
        private static Task<IScheduler> CreateSchedulerAsync()
        {
            NameValueCollection collection = GetQuartzConfig();
            StdSchedulerFactory factory = new StdSchedulerFactory(collection);
            return factory.GetScheduler();
        }

        /// <summary>
        /// 获取调度器：首次调用时创建并缓存。
        /// </summary>
        private Task<IScheduler> GetTaskSchedulerAsync() => _scheduler.Value;

        /// <summary>
        /// 开启计划任务
        /// 参考文章：https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html#datasources-ado-net-jobstores
        /// </summary>
        /// <returns></returns>
        public async Task<ApiResult> StartTaskScheduleAsync()
        {
            var scheduler = await GetTaskSchedulerAsync();
            scheduler.JobFactory = _jobFactory;
            if (scheduler.IsStarted)
            {
                return ApiResult.Error(500, $"计划任务已经开启");
            }

            //等待任务运行完成
            await scheduler.Start();
            return ApiResult.Success("计划任务开启成功");
        }

        /// <summary>
        /// 停止计划任务
        /// </summary>
        /// <returns></returns>
        public async Task<ApiResult> StopTaskScheduleAsync()
        {
            var scheduler = await GetTaskSchedulerAsync();
            if (scheduler.IsShutdown)
            {
                return ApiResult.Error(500, $"计划任务已经停止");
            }

            await scheduler.Shutdown();
            return ApiResult.Success("计划任务已经停止");
        }

        /// <summary>
        /// 添加一个计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        public async Task<ApiResult> AddTaskScheduleAsync(SysTasks tasksQz)
        {
            try
            {
                var scheduler = await GetTaskSchedulerAsync();
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (await scheduler.CheckExists(jobKey))
                {
                    return ApiResult.Error(500, $"该计划任务已经在执行:【{tasksQz.Name}】,请勿重复添加！");
                }
                if (tasksQz?.EndTime <= DateTime.Now)
                {
                    return ApiResult.Error(500, $"结束时间小于当前时间计划将不会被执行");
                }
                if (tasksQz?.TaskType == 3)
                {
                    if (StoredProcedureTaskExecutor.IsProcedureCall(tasksQz.SqlText))
                    {
                        StoredProcedureTaskExecutor.ValidateConfiguration(tasksQz.SqlText, tasksQz.JobParams);
                    }
                    else if (!SqlTaskGuard.IsSafe(tasksQz.SqlText, out var unsafeReason))
                    {
                        throw new CustomException($"SQL 语句未通过安全校验：{unsafeReason}");
                    }
                }
                #region 设置开始时间和结束时间

                tasksQz.BeginTime = tasksQz.BeginTime == null ? DateTime.Now : tasksQz.BeginTime;
                tasksQz.EndTime = tasksQz.EndTime == null ? DateTime.MaxValue.AddDays(-1) : tasksQz.EndTime;

                // 起止时间由 CreateCronTrigger/CreateSimpleTrigger 直接取 BeginTime/EndTime，
                // 原先在此计算的 starRunTime/endRunTime 从未被引用（变量名也拼错），已移除。
                #endregion

                //2、开启调度器。判断任务调度是否开启
                if (!scheduler.IsStarted)
                {
                    await StartTaskScheduleAsync();
                }

                // 3、统一使用分发器，运行时按租户展开执行。
                IJobDetail job = new JobDetailImpl(tasksQz.ID, tasksQz.JobGroup, typeof(Job_Dispatcher));
                job.JobDataMap.Add("TaskId", tasksQz.ID);
                job.JobDataMap.Add("UserName", "system");
                job.JobDataMap.Add("TraceId", App.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"));
                job.JobDataMap.Add("IsManual", 0);
                job.JobDataMap.Add("TriggerSource", "cron");

                //4、创建一个触发器
                //Misfire 策略一律在对应 Builder 内显式指定：Cron 与 Simple 的指令数值语义不同，
                //不能共用一个常量（例如数值 2 对 Cron 是 DoNothing，对 Simple 却是「立即补执行」）。
                ITrigger trigger;
                if (tasksQz.Cron != null && CronExpression.IsValidExpression(tasksQz.Cron))
                {
                    trigger = CreateCronTrigger(tasksQz);
                }
                else
                {
                    trigger = CreateSimpleTrigger(tasksQz);
                }

                // 5、将触发器和任务器绑定到调度器中
                await scheduler.ScheduleJob(job, trigger);
                await scheduler.ResumeTrigger(trigger.Key);

                return ApiResult.Success($"启动计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"启动计划任务失败，分组：{tasksQz.JobGroup},作业：【{tasksQz.Name}】,error：{ex.Message}");
                return ApiResult.Error(500, $"启动计划任务:【{tasksQz.Name}】失败！");
            }
        }

        /// <summary>
        /// 暂停指定的计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        public async Task<ApiResult> PauseTaskScheduleAsync(SysTasks tasksQz)
        {
            try
            {
                var scheduler = await GetTaskSchedulerAsync();
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (await scheduler.CheckExists(jobKey))
                {
                    // 防止创建时存在数据问题 先移除，然后在执行创建操作
                    await scheduler.PauseJob(jobKey);
                }
                return ApiResult.Success($"暂停计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return new ApiResult(500, $"暂停计划任务:【{tasksQz.Name}】失败，{ex.Message}");
            }
        }

        /// <summary>
        /// 恢复指定计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        public async Task<ApiResult> ResumeTaskScheduleAsync(SysTasks tasksQz)
        {
            var scheduler = await GetTaskSchedulerAsync();
            JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
            if (!await scheduler.CheckExists(jobKey))
            {
                return ApiResult.Error(500, $"未找到计划任务:【{tasksQz.Name}】");
            }
            await scheduler.ResumeJob(jobKey);
            return ApiResult.Success($"恢复计划任务:【{tasksQz.Name}】成功");
        }

        /// <summary>
        /// 删除指定计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        public async Task<ApiResult> DeleteTaskScheduleAsync(SysTasks tasksQz)
        {
            try
            {
                var scheduler = await GetTaskSchedulerAsync();
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                await scheduler.DeleteJob(jobKey);
                return ApiResult.Success($"删除计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return ApiResult.Error($"删除计划任务:【{tasksQz.Name}】失败, error={ex.Message}");
            }
        }

        /// <summary>
        /// 立即运行
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <param name="operatorName"></param>
        /// <returns></returns>
        public async Task<ApiResult> RunTaskScheduleAsync(SysTasks tasksQz, string operatorName)
        {
            try
            {
                var scheduler = await GetTaskSchedulerAsync();
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                bool exists = await scheduler.CheckExists(jobKey);

                // Run 只负责立即执行一次，不启动/恢复正式调度；
                // 如需启动任务，请调用 StartTaskScheduleAsync/AddTaskScheduleAsync。
                if (!exists)
                {
                    return await RunTaskOnceAsync(tasksQz, operatorName);
                }

                var triggers = await scheduler.GetTriggersOfJob(jobKey);
                if (triggers.Count <= 0)
                {
                    return new ApiResult(110, $"未找到触发器[{jobKey.Name}]");
                }

                // 手动立即执行时，覆盖本次触发的执行人和标记
                var manualData = new JobDataMap
                {
                    { "TaskId", tasksQz.ID },
                    { "UserName", string.IsNullOrWhiteSpace(operatorName) ? "system" : operatorName },
                    { "TraceId", App.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N") },
                    { "IsManual", 1 },
                    { "TriggerSource", "manual" }
                };
                await scheduler.TriggerJob(jobKey, manualData);

                return ApiResult.Success($"运行计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception ex)
            {
                return new ApiResult(500, $"执行计划任务:【{tasksQz.Name}】失败，{ex.Message}");
            }
        }

        /// <summary>
        /// 任务未启动时，执行一次
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <param name="operatorName"></param>
        /// <returns></returns>
        private async Task<ApiResult> RunTaskOnceAsync(SysTasks tasksQz, string operatorName)
        {
            try
            {
                var scheduler = await GetTaskSchedulerAsync();
                if (!scheduler.IsStarted)
                {
                    await StartTaskScheduleAsync();
                }

                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                TriggerKey triggerKey = new TriggerKey($"{tasksQz.ID}_once_{Guid.NewGuid():N}", tasksQz.JobGroup);

                IJobDetail job = JobBuilder.Create<Job_Dispatcher>()
                    .WithIdentity(jobKey)
                    .StoreDurably(false)
                    .UsingJobData("TaskId", tasksQz.ID)
                    .Build();

                job.JobDataMap.Add("UserName", operatorName);
                job.JobDataMap.Add("TraceId", App.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"));
                // 一次性触发标记为手动执行
                job.JobDataMap.Add("IsManual", 1);
                job.JobDataMap.Add("TriggerSource", "manual");

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithRepeatCount(0).WithMisfireHandlingInstructionFireNow())
                    .Build();

                await scheduler.ScheduleJob(job, trigger);
                return ApiResult.Success($"运行计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"执行一次任务失败，分组：{tasksQz.JobGroup},作业：【{tasksQz.Name}】,error：{ex.Message}");
                return ApiResult.Error(500, $"执行一次任务:【{tasksQz.Name}】失败，{ex.Message}");
            }
        }

        /// <summary>
        /// 更新计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        public async Task<ApiResult> UpdateTaskScheduleAsync(SysTasks tasksQz)
        {
            try
            {
                var scheduler = await GetTaskSchedulerAsync();
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (await scheduler.CheckExists(jobKey))
                {
                    //防止创建时存在数据问题 先移除，然后在执行创建操作
                    await scheduler.DeleteJob(jobKey);
                }
                return await AddTaskScheduleAsync(tasksQz);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return ApiResult.Error($"修改计划失败，error={ex.Message}");
            }
        }

        #region 创建触发器帮助方法

        /// <summary>
        /// 创建SimpleTrigger触发器（简单触发器）
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        private ITrigger CreateSimpleTrigger(SysTasks tasksQz)
        {
            // SimpleTrigger 的 Misfire 策略同样由 MisfirePolicy 决定：
            // 0=NextWithRemainingCount（默认：错过的触发点直接跳到「现在之后的下一个计划点」，不立即补执行，
            //   错过次数计入已执行，等价于 Cron 的 DoNothing：错过即丢失、不追补）
            // 1=FireNow（立即补执行一次）
            // 2=IgnoreMisfires（把错过的全部补上）
            return TriggerBuilder.Create()
                .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                .StartAt(tasksQz.BeginTime.Value)
                .EndAt(tasksQz.EndTime.Value)
                .WithSimpleSchedule(x =>
                {
                    x.WithIntervalInSeconds(tasksQz.IntervalSecond);
                    if (tasksQz.RunTimes > 0)
                    {
                        x.WithRepeatCount(tasksQz.RunTimes);
                    }
                    else
                    {
                        x.RepeatForever();
                    }

                    switch (tasksQz.MisfirePolicy)
                    {
                        case 1: x.WithMisfireHandlingInstructionFireNow(); break;
                        case 2: x.WithMisfireHandlingInstructionIgnoreMisfires(); break;
                        default: x.WithMisfireHandlingInstructionNextWithRemainingCount(); break;
                    }
                })
                .ForJob(tasksQz.ID, tasksQz.JobGroup)
                .Build();
        }

        /// <summary>
        /// 创建类型Cron的触发器
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        private ITrigger CreateCronTrigger(SysTasks tasksQz)
        {
            // 作业触发器
            // 错过触发点如何处理由任务的 MisfirePolicy 决定：
            // 0=DoNothing（默认：等下一个 Cron 时刻，避免调度器启动后立刻跑一次）
            // 1=FireAndProceed（立即补执行一次，之后按计划继续）
            // 2=IgnoreMisfires（把错过的全部补上）
            // 注意：Cron 与 Simple 的 Quartz 指令数值语义不同，这里按策略名映射，不透传数字。
            return TriggerBuilder.Create()
                   .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                   .StartAt(tasksQz.BeginTime.Value)//开始时间
                   .EndAt(tasksQz.EndTime.Value)//结束数据
                   .WithCronSchedule(tasksQz.Cron, x =>
                   {
                       switch (tasksQz.MisfirePolicy)
                       {
                           case 1: x.WithMisfireHandlingInstructionFireAndProceed(); break;
                           case 2: x.WithMisfireHandlingInstructionIgnoreMisfires(); break;
                           default: x.WithMisfireHandlingInstructionDoNothing(); break;
                       }
                   })//指定cron表达式
                   .ForJob(tasksQz.ID, tasksQz.JobGroup)//作业名称
                   .Build();
        }

        #endregion

        /// <summary>
        /// 获取任务运行时快照：正在执行、下次/上次触发时间、暂停状态。
        /// 只读取 Quartz 内存状态，不查数据库；调度器未启动时直接返回空快照。
        /// </summary>
        public async Task<TaskRuntimeSnapshot> GetRuntimeSnapshotAsync(IEnumerable<string> taskIds = null)
        {
            var snapshot = new TaskRuntimeSnapshot();
            var scheduler = await GetTaskSchedulerAsync();
            if (!scheduler.IsStarted)
            {
                return snapshot;
            }

            snapshot.SchedulerStarted = true;

            var filter = taskIds?.Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 正在执行的 JobKey；业务 Job 统一以分发器身份运行，Key.Name 即任务ID
            var running = (await scheduler.GetCurrentlyExecutingJobs())
                .Select(j => j.JobDetail.Key)
                .ToHashSet();

            foreach (var groupName in await scheduler.GetJobGroupNames())
            {
                var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(groupName));
                foreach (var jobKey in jobKeys)
                {
                    if (filter != null && filter.Count > 0 && !filter.Contains(jobKey.Name))
                    {
                        continue;
                    }

                    var triggers = await scheduler.GetTriggersOfJob(jobKey);

                    DateTime? nextFire = null;
                    DateTime? prevFire = null;
                    var paused = false;

                    if (triggers.Count > 0)
                    {
                        var nexts = triggers.Select(t => t.GetNextFireTimeUtc())
                            .Where(t => t.HasValue).Select(t => t.Value).ToList();
                        if (nexts.Count > 0)
                        {
                            nextFire = nexts.Min().LocalDateTime;
                        }

                        var prevs = triggers.Select(t => t.GetPreviousFireTimeUtc())
                            .Where(t => t.HasValue).Select(t => t.Value).ToList();
                        if (prevs.Count > 0)
                        {
                            prevFire = prevs.Max().LocalDateTime;
                        }

                        var states = await Task.WhenAll(triggers.Select(t => scheduler.GetTriggerState(t.Key)));
                        paused = states.All(s => s == TriggerState.Paused);
                    }

                    snapshot.Tasks.Add(new TaskRuntimeInfo
                    {
                        TaskId = jobKey.Name,
                        JobGroup = jobKey.Group,
                        IsRunning = running.Contains(jobKey),
                        Paused = paused,
                        NextFireTime = nextFire,
                        PrevFireTime = prevFire,
                        TriggerCount = triggers.Count
                    });
                }
            }

            return snapshot;
        }
    }
}
