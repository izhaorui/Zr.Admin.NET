using Infrastructure;
using Infrastructure.Model;
using NLog;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
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
        private Task<IScheduler> _scheduler;
        private readonly IJobFactory _jobFactory;
        /// <summary>
        /// 日志接口
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public TaskSchedulerServer(IJobFactory jobFactory)
        {
            _scheduler = GetTaskSchedulerAsync();
            _jobFactory = jobFactory;
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

            // Quartz 3.x 推荐 json；binary 依赖的 BinaryFormatter 已从 .NET 9+ 运行时移除，
            // net10 下启用持久化 JobStore 会直接抛 PlatformNotSupportedException。
            if (string.IsNullOrWhiteSpace(collection["quartz.serializer.type"]))
            {
                collection["quartz.serializer.type"] = "json";
            }

            return collection;
        }

        /// <summary>
        /// 开启计划任务
        /// 参考文章：https://www.quartz-scheduler.net/documentation/quartz-3.x/configuration/reference.html#datasources-ado-net-jobstores
        /// </summary>
        /// <returns></returns>
        private Task<IScheduler> GetTaskSchedulerAsync()
        {
            if (_scheduler != null)
            {
                return _scheduler;
            }

            NameValueCollection collection = GetQuartzConfig();
            StdSchedulerFactory factory = new StdSchedulerFactory(collection);

            return _scheduler = factory.GetScheduler();
        }

        public async Task<ApiResult> StartTaskScheduleAsync()
        {
            try
            {
                _scheduler.Result.JobFactory = _jobFactory;
                if (_scheduler.Result.IsStarted)
                {
                    return ApiResult.Error(500, $"计划任务已经开启");
                }

                //等待任务运行完成
                await _scheduler.Result.Start();
                return ApiResult.Success("计划任务开启成功");
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 停止计划任务
        /// </summary>
        /// <returns></returns>
        public async Task<ApiResult> StopTaskScheduleAsync()
        {
            try
            {
                if (_scheduler.Result.IsShutdown)
                {
                    return ApiResult.Error(500, $"计划任务已经停止");
                }

                await _scheduler.Result.Shutdown();
                return ApiResult.Success("计划任务已经停止");
            }
            catch (Exception)
            {
                throw;
            }
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
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (await _scheduler.Result.CheckExists(jobKey))
                {
                    return ApiResult.Error(500, $"该计划任务已经在执行:【{tasksQz.Name}】,请勿重复添加！");
                }
                if (tasksQz?.EndTime <= DateTime.Now)
                {
                    return ApiResult.Error(500, $"结束时间小于当前时间计划将不会被执行");
                }
                #region 设置开始时间和结束时间

                tasksQz.BeginTime = tasksQz.BeginTime == null ? DateTime.Now : tasksQz.BeginTime;
                tasksQz.EndTime = tasksQz.EndTime == null ? DateTime.MaxValue.AddDays(-1) : tasksQz.EndTime;

                DateTimeOffset starRunTime = DateBuilder.NextGivenSecondDate(tasksQz.BeginTime, 1);//设置开始时间
                DateTimeOffset endRunTime = DateBuilder.NextGivenSecondDate(tasksQz.EndTime, 1);//设置暂停时间

                #endregion

                //2、开启调度器。判断任务调度是否开启
                if (!_scheduler.Result.IsStarted)
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
                await _scheduler.Result.ScheduleJob(job, trigger);
                await _scheduler.Result.ResumeTrigger(trigger.Key);

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
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (await _scheduler.Result.CheckExists(jobKey))
                {
                    // 防止创建时存在数据问题 先移除，然后在执行创建操作
                    await _scheduler.Result.PauseJob(jobKey);
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
            try
            {
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (!await _scheduler.Result.CheckExists(jobKey))
                {
                    return ApiResult.Error(500, $"未找到计划任务:【{tasksQz.Name}】");
                }
                await _scheduler.Result.ResumeJob(jobKey);
                return ApiResult.Success($"恢复计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception)
            {
                throw;
            }
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
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                await _scheduler.Result.DeleteJob(jobKey);
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
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                bool exists = await _scheduler.Result.CheckExists(jobKey);

                // Run 只负责立即执行一次，不启动/恢复正式调度；
                // 如需启动任务，请调用 StartTaskScheduleAsync/AddTaskScheduleAsync。
                if (!exists)
                {
                    return await RunTaskOnceAsync(tasksQz, operatorName);
                }

                var triggers = await _scheduler.Result.GetTriggersOfJob(jobKey);
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
                await _scheduler.Result.TriggerJob(jobKey, manualData);

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
                if (!_scheduler.Result.IsStarted)
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

                await _scheduler.Result.ScheduleJob(job, trigger);
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
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                if (await _scheduler.Result.CheckExists(jobKey))
                {
                    //防止创建时存在数据问题 先移除，然后在执行创建操作
                    await _scheduler.Result.DeleteJob(jobKey);
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
            // SimpleTrigger 没有 DoNothing 指令，这里显式选用 NextWithRemainingCount：
            // 错过的触发点直接跳到「现在之后的下一个计划点」，不立即补执行，
            // 错过次数计入已执行（等价于 Cron 的 DoNothing：错过即丢失，不会追补）。
            if (tasksQz.RunTimes > 0)
            {
                ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                .StartAt(tasksQz.BeginTime.Value)
                .EndAt(tasksQz.EndTime.Value)
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(tasksQz.IntervalSecond)
                .WithRepeatCount(tasksQz.RunTimes)
                .WithMisfireHandlingInstructionNextWithRemainingCount()).ForJob(tasksQz.ID, tasksQz.JobGroup).Build();
                return trigger;
            }
            else
            {
                ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                .StartAt(tasksQz.BeginTime.Value)
                .EndAt(tasksQz.EndTime.Value)
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(tasksQz.IntervalSecond)
                .RepeatForever()
                .WithMisfireHandlingInstructionNextWithRemainingCount()).ForJob(tasksQz.ID, tasksQz.JobGroup).Build();
                return trigger;
            }
            // 触发作业立即运行，然后每10秒重复一次，无限循环
        }

        /// <summary>
        /// 创建类型Cron的触发器
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        private ITrigger CreateCronTrigger(SysTasks tasksQz)
        {
            // 作业触发器
            // DoNothing：错过触发点不做补执行，等下一个 Cron 时刻，避免调度器启动后立刻跑一次。
            return TriggerBuilder.Create()
                   .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                   .StartAt(tasksQz.BeginTime.Value)//开始时间
                   .EndAt(tasksQz.EndTime.Value)//结束数据
                   .WithCronSchedule(tasksQz.Cron, x => x.WithMisfireHandlingInstructionDoNothing())//指定cron表达式
                   .ForJob(tasksQz.ID, tasksQz.JobGroup)//作业名称
                   .Build();
        }

        #endregion

    }
}
