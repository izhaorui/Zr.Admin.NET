using Infrastructure;
using NLog;
using Quartz;
using System;
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
        public async Task<SysTasksLog> ExecuteJob(IJobExecutionContext context, Func<Task> job)
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
                logMsg = "success";
            }
            catch (Exception ex)
            {
                JobExecutionException e2 = new(ex)
                {
                    //true  是立即重新执行任务 
                    RefireImmediately = true
                };
                status = 1;
                logMsg = $"Fail，Exception：{ex.Message}";
            }

            var logModel = new SysTasksLog()
            {
                Elapsed = elapsed,
                Status = status.ToString(),
                JobMessage = logMsg
            };

            await RecordTaskLog(context, logModel);
            return logModel;
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

            logModel.InvokeTarget = job.JobType.FullName;
            logModel = await tasksLogService.AddTaskLog(job.Key.Name, logModel);
            //成功后执行次数+1
            if (logModel.Status == "0")
            {
                await taskQzService.UpdateAsync(f => new SysTasksQz()
                {
                    RunTimes = f.RunTimes + 1,
                    LastRunTime = DateTime.Now
                }, f => f.ID == job.Key.Name);
            }
            logger.Info($"执行任务【{job.Key.Name}|{logModel.JobName}】结果={logModel.JobMessage}");
        }
    }
}
