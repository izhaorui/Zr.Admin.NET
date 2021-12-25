using Infrastructure;
using NLog;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Tasks
{
    public class JobBase
    {
        /// <summary>
        /// 日志接口
        /// </summary>
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 执行指定任务
        /// </summary>
        /// <param name="context">作业上下文</param>
        /// <param name="job">业务逻辑方法</param>
        public async Task<Dictionary<string,object>> ExecuteJob(IJobExecutionContext context, Func<Task> job)
        {
            double elapsed = 0;
            int status = 0;
            string logMsg;
            try
            {
                //var s = context.Trigger.Key.Name;
                //记录Job时间
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                //执行任务
                await job();
                stopwatch.Stop();
                elapsed = stopwatch.Elapsed.TotalMilliseconds;
                logMsg = "Succeed";
            }
            catch (Exception ex)
            {
                JobExecutionException e2 = new JobExecutionException(ex);
                //true  是立即重新执行任务 
                e2.RefireImmediately = true;
                status = 1;
                logMsg = $"Fail，Exception：{ex.Message}";
            }
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic.Add("elapsed", elapsed);
            dic.Add("status", status);
            dic.Add("content", logMsg);

            RecordTaskLog(context, dic);
            return dic;
        }

        /// <summary>
        /// 记录到日志
        /// </summary>
        /// <param name="context"></param>
        /// <param name="executeLog"></param>
        protected void RecordTaskLog(IJobExecutionContext context, Dictionary<string, object> executeLog)
        {
            var tasksLogService = (ISysTasksLogService)App.GetRequiredService(typeof(ISysTasksLogService));
            var taskQzService = (ISysTasksQzService)App.GetRequiredService(typeof(ISysTasksQzService));

            // 可以直接获取 JobDetail 的值
            IJobDetail job = context.JobDetail;
            //var param = context.MergedJobDataMap;

            // 也可以通过数据库配置，获取传递过来的参数
            //JobDataMap data = context.JobDetail.JobDataMap;
            //int jobId = data.GetInt("JobParam");

            var logModel = new SysTasksLog();

            logModel.InvokeTarget = job.JobType.FullName;
            logModel.Elapsed = (double)executeLog.GetValueOrDefault("elapsed", "0");
            logModel.JobMessage = executeLog.GetValueOrDefault("content").ToString();
            logModel.Status = executeLog.GetValueOrDefault("status", "0").ToString();
            logModel = tasksLogService.AddTaskLog(job.Key.Name, logModel);
            taskQzService.Update(f => f.ID == job.Key.Name, f => new SysTasksQz()
            {
                RunTimes = f.RunTimes + 1
            });
            logger.Info($"执行任务【{job.Key.Name}|{logModel.JobName}】结果={logModel.JobMessage}");
        }
    }
}
