using Infrastructure.Model;
using NLog;
using Quartz.Spi;
using SqlSugar;
using SqlSugar.IOC;
using ZR.Model.System;
using ZR.Tasks;

namespace ZR.Admin.WebApi.Extensions
{
    /// <summary>
    /// 定时任务扩展方法
    /// </summary>
    public static class TasksExtension
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 注册任务
        /// </summary>
        /// <param name="services"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AddTaskSchedulers(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            //添加Quartz服务
            services.AddSingleton<IJobFactory, JobFactory>();
            services.AddTransient<ITaskSchedulerServer, TaskSchedulerServer>();
        }

        /// <summary>
        /// 程序启动后添加任务计划。
        /// </summary>
        /// <remarks>
        /// 失败不抛出：注册任务只是宿主启动的附加动作，库不可用或某个任务配置损坏时
        /// 不能让整个站点点不起来，异常在此处收口并落日志。
        /// </remarks>
        /// <param name="app"></param>
        /// <returns></returns>
        public static async Task<IApplicationBuilder> UseAddTaskSchedulers(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            ITaskSchedulerServer schedulerServer = app.ApplicationServices.GetRequiredService<ITaskSchedulerServer>();

            List<SysTasks> tasks;
            try
            {
                tasks = await DbScoped.SugarScope.Queryable<SysTasks>()
                    .Where(m => m.IsStart == 1).ToListAsync();
            }
            catch (Exception ex)
            {
                // 库不可用 / 连接串错误：本次一个任务都不注册，站点继续启动
                logger.Error(ex, "加载定时任务列表失败，本次跳过全部任务注册");
                return app;
            }

            //程序启动后注册所有定时任务
            foreach (var task in tasks)
            {
                try
                {
                    var result = await schedulerServer.AddTaskScheduleAsync(task);
                    if (result.IsSuccess())
                    {
                        logger.Info("注册任务[{0}]ID：{1}成功", task.Name, task.ID);
                    }
                    else
                    {
                        // 失败必须留痕：cron 非法、结束时间已过、JobKey 冲突都会走到这里。
                        // 原实现只在成功时输出，失败时现场没有任何记录。
                        logger.Warn("注册任务[{0}]ID：{1}失败：{2}", task.Name, task.ID, result[ApiResult.MSG_TAG]);
                    }
                }
                catch (Exception ex)
                {
                    // 单个任务异常不影响其余任务注册
                    logger.Error(ex, "注册任务[{0}]ID：{1}异常", task.Name, task.ID);
                }
            }

            return app;
        }

        /// <summary>
        /// 初始化字典
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseInit(this IApplicationBuilder app)
        {
            var mainDb = App.MainDbConfigId;
            //var tenantId = App.GetCurrentTenantId();
            //Console.WriteLine("初始化字典数据...");
            var db = DbScoped.SugarScope.GetConnection(mainDb);
            var types = db.Queryable<SysDictType>()
                .Where(it => it.Status == "0")
                .Select(it => it.DictType)
                .ToList();

            //上面有耗时操作写在Any上面，保证程序启动后只执行一次
            if (!db.ConfigQuery.Any())
            {
                foreach (var type in types)
                {
                    db.ConfigQuery.SetTable<SysDictData>(it => SqlFunc.ToString(it.DictValue), it => it.DictLabel, type, it => it.DictType == type);
                }
            }
            return app;
        }
    }
}
