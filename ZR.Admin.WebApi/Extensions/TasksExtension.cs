using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Quartz.Spi;
using System;
using ZR.Tasks;

namespace ZR.Admin.WebApi.Extensions
{
    /// <summary>
    /// 定时任务扩展方法
    /// </summary>
    public static class TasksExtension
    {
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
            //var _tasksQzService = (ISysTasksQzService)App.GetRequiredService(typeof(ISysTasksQzService));

            ITaskSchedulerServer _schedulerServer = app.ApplicationServices.GetRequiredService<ITaskSchedulerServer>();

            //var tasks = _tasksQzService.GetList(m => m.IsStart);
            var tasks = SqlSugar.IOC.DbScoped.SugarScope.Queryable<Model.System.SysTasks>().Where(m => m.IsStart).ToList();

            //程序启动后注册所有定时任务
            foreach (var task in tasks)
            {
                var result = _schedulerServer.AddTaskScheduleAsync(task);
                if (result.Result.Code == 200)
                {
                    Console.WriteLine($"注册任务[{task.Name}]ID：{task.ID}成功");
                }
            }

            return app;
        }

    }
}
