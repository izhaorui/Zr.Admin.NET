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
        /// 程序启动后添加任务计划
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseAddTaskSchedulers(this IApplicationBuilder app)
        {
            ITaskSchedulerServer _schedulerServer = app.ApplicationServices.GetRequiredService<ITaskSchedulerServer>();

            var tasks = DbScoped.SugarScope.Queryable<SysTasks>()
                .Where(m => m.IsStart == 1).ToListAsync();

            //程序启动后注册所有定时任务
            foreach (var task in tasks.Result)
            {
                var result = _schedulerServer.AddTaskScheduleAsync(task);
                if (result.Result.IsSuccess())
                {
                    Console.WriteLine($"注册任务[{task.Name}]ID：{task.ID}成功");
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
            //Console.WriteLine("初始化字典数据...");
            var db = DbScoped.SugarScope;
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
