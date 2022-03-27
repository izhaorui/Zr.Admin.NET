using Infrastructure.Model;
using NLog;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
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

            NameValueCollection collection = new NameValueCollection
            {
                { "quartz.serializer.type","binary" },
                //quartz参数
                //{ "quartz.scheduler.instanceId", "AUTO" },
                //{ "quartz.serializer.type","json" },
                ////下面为指定quartz持久化数据库的配置
                //{ "quartz.jobStore.type","Quartz.Impl.AdoJobStore.JobStoreTX, Quartz" },
                //{ "quartz.jobStore.tablePrefix","QRTZ_"},
                //{ "quartz.jobStore.driverDelegateType", "Quartz.Impl.AdoJobStore.MySQLDelegate, Quartz"},
                //{ "quartz.jobStore.useProperties", "true"},
                //{ "quartz.jobStore.dataSource", "myDS" },
                //{ "quartz.dataSource.myDS.connectionString", @"server=xxx.xxx.xxx.xxx;port=3306;database=Admin;uid=zrry;pwd=********;Charset=utf8;"},
                //{ "quartz.dataSource.myDS.provider", "MySql" },
            };

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
        public async Task<ApiResult> AddTaskScheduleAsync(SysTasksQz tasksQz)
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
        public async Task<ApiResult> PauseTaskScheduleAsync(SysTasksQz tasksQz)
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
        public async Task<ApiResult> ResumeTaskScheduleAsync(SysTasksQz tasksQz)
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
        public async Task<ApiResult> DeleteTaskScheduleAsync(SysTasksQz tasksQz)
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
        /// <returns></returns>
        public async Task<ApiResult> RunTaskScheduleAsync(SysTasksQz tasksQz)
        {
            try
            {
                JobKey jobKey = new JobKey(tasksQz.ID, tasksQz.JobGroup);
                List<JobKey> jobKeys = _scheduler.Result.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(tasksQz.JobGroup)).Result.ToList();
                if (jobKeys == null || jobKeys.Count == 0)
                {
                    await AddTaskScheduleAsync(tasksQz);
                }

                var triggers = await _scheduler.Result.GetTriggersOfJob(jobKey);
                if (triggers.Count <= 0)
                {
                    return new ApiResult(110, $"未找到触发器[{jobKey.Name}]");
                }
                await _scheduler.Result.TriggerJob(jobKey);

                return ApiResult.Success($"运行计划任务:【{tasksQz.Name}】成功");
            }
            catch (Exception ex)
            {
                return new ApiResult(500, $"执行计划任务:【{tasksQz.Name}】失败，{ex.Message}");
            }
        }

        /// <summary>
        /// 更新计划任务
        /// </summary>
        /// <param name="tasksQz"></param>
        /// <returns></returns>
        public async Task<ApiResult> UpdateTaskScheduleAsync(SysTasksQz tasksQz)
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
        private ITrigger CreateSimpleTrigger(SysTasksQz tasksQz)
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
        private ITrigger CreateCronTrigger(SysTasksQz tasksQz)
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
