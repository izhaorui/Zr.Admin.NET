using Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Model;
using NLog;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;
using Quartz.Spi;
using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading.Tasks;
using ZR.Model.System;

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

            if (string.IsNullOrWhiteSpace(collection["quartz.serializer.type"]))
            {
                collection["quartz.serializer.type"] = "binary";
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

                #region 通过反射获取程序集类型和类   

                Assembly assembly = Assembly.Load(new AssemblyName(tasksQz.AssemblyName));
                Type jobType = assembly.GetType(tasksQz.AssemblyName + "." + tasksQz.ClassName);

                #endregion
                //2、开启调度器。判断任务调度是否开启
                if (!_scheduler.Result.IsStarted)
                {
                    await StartTaskScheduleAsync();
                }

                //3、创建任务。传入反射出来的执行程序集
                IJobDetail job = new JobDetailImpl(tasksQz.ID, tasksQz.JobGroup, jobType);
                job.JobDataMap.Add("JobParam", tasksQz.JobParams);
                job.JobDataMap.Add("UserName", "system");
                job.JobDataMap.Add("TraceId", App.HttpContext?.TraceIdentifier ?? System.Guid.NewGuid().ToString("N"));
                // 标记为系统调度的任务
                job.JobDataMap.Add("IsManual", 0);
                job.JobDataMap.Add("TriggerSource", "cron");
                job.JobDataMap.Add("TenantId", tasksQz.TenantId);

                ITrigger trigger;

                //4、创建一个触发器
                if (tasksQz.Cron != null && CronExpression.IsValidExpression(tasksQz.Cron))
                {
                    trigger = CreateCronTrigger(tasksQz);
                    //解决Quartz启动后第一次会立即执行问题解决办法
                    ((CronTriggerImpl)trigger).MisfireInstruction = MisfireInstruction.CronTrigger.DoNothing;
                }
                else
                {
                    trigger = CreateSimpleTrigger(tasksQz);
                    ((SimpleTriggerImpl)trigger).MisfireInstruction = MisfireInstruction.CronTrigger.DoNothing;
                }

                // 5、将触发器和任务器绑定到调度器中
                await _scheduler.Result.ScheduleJob(job, trigger);
                //任务没有启动、暂停任务
                //if (!tasksQz.IsStart)
                //{
                //    _scheduler.Result.PauseJob(jobKey).Wait();
                //}
                //按新的trigger重新设置job执行
                await _scheduler.Result.ResumeTrigger(trigger.Key);
                return ApiResult.Success($"启动计划任务:【{tasksQz.Name}】成功！");
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

                //await _scheduler.Result.ScheduleJob()

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
                if (!exists)
                {
                    if (tasksQz.IsStart == 1)
                    {
                        await AddTaskScheduleAsync(tasksQz);
                    }
                    else
                    {
                        return await RunTaskOnceAsync(tasksQz, operatorName);
                    }
                }

                var triggers = await _scheduler.Result.GetTriggersOfJob(jobKey);
                if (triggers.Count <= 0)
                {
                    return new ApiResult(110, $"未找到触发器[{jobKey.Name}]");
                }

                // 手动立即执行时，覆盖本次触发的执行人和标记
                var manualData = new JobDataMap
                {
                    { "UserName", string.IsNullOrWhiteSpace(operatorName) ? "system" : operatorName },
                    { "TraceId", App.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N") },
                    { "IsManual", 1 },
                    { "TriggerSource", "manual" },
                    { "TenantId", tasksQz.TenantId }
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

                Assembly assembly = Assembly.Load(new AssemblyName(tasksQz.AssemblyName));
                Type jobType = assembly.GetType(tasksQz.AssemblyName + "." + tasksQz.ClassName);
                if (jobType == null)
                {
                    return ApiResult.Error(500, $"未找到任务类型: {tasksQz.AssemblyName}.{tasksQz.ClassName}");
                }

                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                TriggerKey triggerKey = new TriggerKey($"{tasksQz.ID}_once_{Guid.NewGuid():N}", tasksQz.JobGroup);

                IJobDetail job = JobBuilder.Create(jobType)
                    .WithIdentity(jobKey)
                    .UsingJobData("JobParam", tasksQz.JobParams ?? string.Empty)
                    .Build();

                job.JobDataMap.Add("UserName", operatorName);
                job.JobDataMap.Add("TraceId", App.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N"));
                // 一次性触发标记为手动执行
                job.JobDataMap.Add("IsManual", 1);
                job.JobDataMap.Add("TriggerSource", "manual");
                job.JobDataMap.Add("TenantId", tasksQz.TenantId);

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(1).WithRepeatCount(0))
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
                //await AddTaskScheduleAsync(tasksQz);
                return ApiResult.Success("修改计划成功");
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
            if (tasksQz.RunTimes > 0)
            {
                ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                .StartAt(tasksQz.BeginTime.Value)
                .EndAt(tasksQz.EndTime.Value)
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(tasksQz.IntervalSecond)
                .WithRepeatCount(tasksQz.RunTimes)).ForJob(tasksQz.ID, tasksQz.JobGroup).Build();
                return trigger;
            }
            else
            {
                ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                .StartAt(tasksQz.BeginTime.Value)
                .EndAt(tasksQz.EndTime.Value)
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(tasksQz.IntervalSecond)
                .RepeatForever()).ForJob(tasksQz.ID, tasksQz.JobGroup).Build();
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
            return TriggerBuilder.Create()
                   .WithIdentity(tasksQz.ID, tasksQz.JobGroup)
                   .StartAt(tasksQz.BeginTime.Value)//开始时间
                   .EndAt(tasksQz.EndTime.Value)//结束数据
                   .WithCronSchedule(tasksQz.Cron)//指定cron表达式
                   .ForJob(tasksQz.ID, tasksQz.JobGroup)//作业名称
                   .Build();
        }

        #endregion

    }
}
