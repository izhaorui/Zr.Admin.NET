using Infrastructure;
using NLog;
using Quartz;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ZR.Common;
using ZR.Model.System;
using ZR.ServiceCore.Services;

namespace ZR.Tasks
{
    public class JobBase
    {
        /// <summary>
        /// 日志接口
        /// </summary>
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const int MaxJobMessageLength = 2000;
        private const string SuccessMessage = "success";

        /// <summary>
        /// 执行指定任务
        /// </summary>
        /// <param name="context">作业上下文</param>
        /// <param name="job">业务逻辑方法</param>
        public async Task<SysTasksLog> ExecuteJob(IJobExecutionContext context, Func<Task> job)
        {
            return await ExecuteJobInternal(context, async () =>
            {
                await job();
                return SuccessMessage;
            }, preserveQuartzException: false);
        }

        /// <summary>
        /// 执行指定任务（接收返回结果）
        /// </summary>
        /// <param name="context">作业上下文</param>
        /// <param name="job">业务逻辑方法</param>
        public async Task<SysTasksLog> ExecuteJob(IJobExecutionContext context, Func<Task<string>> job)
        {
            return await ExecuteJobInternal(context, job, preserveQuartzException: true);
        }

        private async Task<SysTasksLog> ExecuteJobInternal(
            IJobExecutionContext context,
            Func<Task<string>> job,
            bool preserveQuartzException)
        {
            var stopwatch = Stopwatch.StartNew();
            int status = 0;
            string logMsg;
            string tenantId = context.MergedJobDataMap.GetString("TenantId");
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                logger.Warn("任务执行时未携带 TenantId，任务可能在默认/空租户上下文中运行");
            }
            Exception jobException = null;
            using var tenantScope = TenantContext.Change(tenantId);
            try
            {
                var result = await job();
                logMsg = TruncateMessage(result);
            }
            catch (Exception ex)
            {
                status = 1;
                jobException = ex;
                logMsg = TruncateMessage($"Job Run Fail，Exception：{ex.Message}");
                logger.Error(ex, "任务执行出错");
                WxNoticeHelper.SendMsg("任务执行出错", logMsg);
            }
            finally
            {
                stopwatch.Stop();
            }

            var logModel = new SysTasksLog
            {
                Elapsed = stopwatch.Elapsed.TotalMilliseconds,
                Status = status.ToString(),
                JobMessage = logMsg,
                TenantId = tenantId
            };

            await RecordTaskLog(context, logModel);

            if (jobException != null)
            {
                if (preserveQuartzException && jobException is JobExecutionException quartzEx)
                {
                    throw quartzEx;
                }

                throw new JobExecutionException(jobException)
                {
                    // 仅立即重试一次，避免持续失败导致无限重试
                    RefireImmediately = context.RefireCount < 1
                };
            }

            return logModel;
        }

        private static string TruncateMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return SuccessMessage;
            }
            return message.Length <= MaxJobMessageLength ? message : message[..MaxJobMessageLength];
        }

        /// <summary>
        /// 记录到日志
        /// </summary>
        /// <param name="context"></param>
        /// <param name="logModel"></param>
        protected async Task RecordTaskLog(IJobExecutionContext context, SysTasksLog logModel)
        {
            var tasksLogService = (ISysTasksLogService)App.GetRequiredService(typeof(ISysTasksLogService));
            var taskQzService = (ISysTasksQzService)App.GetRequiredService(typeof(ISysTasksQzService));

            // 可以直接获取 JobDetail 的值
            IJobDetail job = context.JobDetail;
            var now = DateTime.Now;

            logModel.InvokeTarget = job.JobType.FullName;
            var mergedData = context.MergedJobDataMap;
            logModel.TraceId = mergedData.GetString("TraceId") ?? Activity.Current?.TraceId.ToString();
            logModel.Operator = mergedData.GetString("UserName");
            try
            {
                var isManualObj = mergedData.ContainsKey("IsManual") ? mergedData["IsManual"] : null;
                if (isManualObj != null)
                {
                    logModel.IsManual = Convert.ToInt32(isManualObj);
                }
            }
            catch { }

            logModel.TriggerSource = mergedData.GetString("TriggerSource");
            logModel = await tasksLogService.AddTaskLog(job.Key.Name, logModel);

            await taskQzService.UpdateAsync(f => new SysTasks()
            {
                RunTimes = f.RunTimes + 1,
                LastRunTime = now,
                LastRunStatus = logModel.Status,
                LastErrorMsg = logModel.Status == "1" ? logModel.JobMessage : null,
                LastFailTime = logModel.Status == "1" ? now : f.LastFailTime,
                LastSuccessTime = logModel.Status == "0" ? now : f.LastSuccessTime
            }, f => f.ID == job.Key.Name);

            logger.Info($"执行任务【{job.Key.Name}|{logModel.JobName}】结果={logModel.JobMessage}");
        }
    }
}
