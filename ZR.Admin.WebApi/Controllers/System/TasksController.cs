using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using SqlSugar;
using System;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Service.System.IService;
using ZR.Tasks;
using Snowflake.Core;
using Infrastructure.Extensions;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 计划任务
    /// </summary>
    [Verify]
    [Route("system/Tasks")]
    public class TasksController : BaseController
    {
        private ISysTasksQzService _tasksQzService;
        private ITaskSchedulerServer _schedulerServer;

        public TasksController(
            ISysTasksQzService sysTasksQzService,
            ITaskSchedulerServer taskScheduler)
        {
            _tasksQzService = sysTasksQzService;
            _schedulerServer = taskScheduler;
        }

        /// <summary>
        /// 查询计划任务列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "monitor:job:list")]
        public IActionResult Query([FromQuery] TasksQueryDto parm, [FromQuery] PagerInfo pager)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<SysTasksQz>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.QueryText),
                m => m.Name.Contains(parm.QueryText) ||
                m.JobGroup.Contains(parm.QueryText) ||
                m.AssemblyName.Contains(parm.QueryText));

            var response = _tasksQzService.GetPages(predicate.ToExpression(), pager, f => f.IsStart, OrderByType.Desc);

            return SUCCESS(response, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 查询单个计划任务
        /// </summary>
        /// <param name="id">编码</param>
        /// <returns></returns>
        [HttpGet("get")]
        public IActionResult Get(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                return SUCCESS(_tasksQzService.GetId(id));
            }
            return SUCCESS(null);
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <returns></returns>
        [HttpPost("create")]
        [ActionPermissionFilter(Permission = "monitor:job:add")]
        [Log(Title = "添加任务", BusinessType = BusinessType.INSERT)]
        public IActionResult Create([FromBody] TasksCreateDto parm)
        {
            //判断是否已经存在
            if (_tasksQzService.Any(m => m.Name == parm.Name))
            {
                throw new CustomException($"添加 {parm.Name} 失败，该用任务存在，不能重复！");
            }
            if (!string.IsNullOrEmpty(parm.Cron) && !CronExpression.IsValidExpression(parm.Cron))
            {
                throw new CustomException($"cron表达式不正确");
            }
            if (string.IsNullOrEmpty(parm.ApiUrl) && parm.TaskType == 2)
            {
                throw new CustomException($"地址不能为空");
            }
            if (parm.TaskType == 1 && (parm.AssemblyName.IsEmpty() || parm.ClassName.IsEmpty()))
            {
                throw new CustomException($"程序集或者类名不能为空");
            }
            //从 Dto 映射到 实体
            var tasksQz = parm.Adapt<SysTasksQz>().ToCreate();
            var worker = new IdWorker(1, 1);

            tasksQz.ID = worker.NextId().ToString();
            tasksQz.IsStart = false;
            tasksQz.Create_by = HttpContext.GetName();
            tasksQz.TaskType = parm.TaskType;
            tasksQz.ApiUrl = parm.ApiUrl;
            if (parm.ApiUrl.IfNotEmpty() && parm.TaskType == 2)
            {
                tasksQz.AssemblyName = "ZR.Tasks";
                tasksQz.ClassName = "TaskScheduler.Job_HttpRequest";
            }
            return SUCCESS(_tasksQzService.Add(tasksQz));
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <returns></returns>
        [HttpPost("update")]
        [ActionPermissionFilter(Permission = "monitor:job:edit")]
        [Log(Title = "修改任务", BusinessType = BusinessType.UPDATE)]
        public async Task<IActionResult> Update([FromBody] TasksUpdateDto parm)
        {
            //判断是否已经存在
            if (_tasksQzService.Any(m => m.Name == parm.Name && m.ID != parm.ID))
            {
                throw new CustomException($"更新 {parm.Name} 失败，该用任务存在，不能重复！");
            }
            if (string.IsNullOrEmpty(parm.Cron) && parm.TriggerType == 1)
            {
                throw new CustomException($"触发器 Corn 模式下，运行时间表达式必须填写");
            }
            if (!string.IsNullOrEmpty(parm.Cron) && !CronExpression.IsValidExpression(parm.Cron))
            {
                throw new CustomException($"cron表达式不正确");
            }
            var tasksQz = _tasksQzService.GetFirst(m => m.ID == parm.ID);
            if (string.IsNullOrEmpty(parm.ApiUrl) && parm.TaskType == 2)
            {
                throw new CustomException($"api地址不能为空");
            }
            if (parm.ApiUrl.IfNotEmpty() && parm.TaskType == 2)
            {
                parm.AssemblyName = "ZR.Tasks";
                parm.ClassName = "TaskScheduler.Job_HttpRequest";
            }
            if (tasksQz.IsStart)
            {
                throw new CustomException($"该任务正在运行中，请先停止在更新");
            }

            var response = _tasksQzService.Update(m => m.ID == parm.ID, m => new SysTasksQz
            {
                Name = parm.Name,
                JobGroup = parm.JobGroup,
                Cron = parm.Cron,
                AssemblyName = parm.AssemblyName,
                ClassName = parm.ClassName,
                Remark = parm.Remark,
                TriggerType = parm.TriggerType,
                IntervalSecond = parm.IntervalSecond,
                JobParams = parm.JobParams,
                Update_by = HttpContextExtension.GetName(HttpContext),
                Update_time = DateTime.Now,
                BeginTime = parm.BeginTime,
                EndTime = parm.EndTime,
                TaskType = parm.TaskType,
                ApiUrl = parm.ApiUrl,
            });
            if (response > 0)
            {
                //先暂停原先的任务
                var respon = await _schedulerServer.UpdateTaskScheduleAsync(tasksQz);
            }

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        /// <returns></returns>
        [HttpDelete("delete")]
        [ActionPermissionFilter(Permission = "monitor:job:delete")]
        [Log(Title = "删除任务", BusinessType = BusinessType.DELETE)]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new CustomException("删除任务 Id 不能为空");
            }

            if (!_tasksQzService.Any(m => m.ID == id))
            {
                throw new CustomException("任务不存在");
            }

            var tasksQz = _tasksQzService.GetFirst(m => m.ID == id);
            var taskResult = await _schedulerServer.DeleteTaskScheduleAsync(tasksQz);

            if (taskResult.Code == 200)
            {
                _tasksQzService.Delete(id);
            }
            return ToResponse(taskResult);
        }

        /// <summary>
        /// 启动任务
        /// </summary>
        /// <returns></returns>
        [HttpGet("start")]
        [ActionPermissionFilter(Permission = "monitor:job:start")]
        [Log(Title = "启动任务", BusinessType = BusinessType.OTHER)]
        public async Task<IActionResult> Start(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new CustomException("启动任务 Id 不能为空");
            }

            if (!_tasksQzService.Any(m => m.ID == id))
            {
                throw new CustomException("任务不存在");
            }

            var tasksQz = _tasksQzService.GetFirst(m => m.ID == id);
            var taskResult = await _schedulerServer.AddTaskScheduleAsync(tasksQz);

            if (taskResult.Code == 200)
            {
                tasksQz.IsStart = true;
                _tasksQzService.Update(tasksQz);
            }

            return ToResponse(taskResult);
        }

        /// <summary>
        /// 停止任务
        /// </summary>
        /// <returns></returns>
        [HttpGet("stop")]
        [ActionPermissionFilter(Permission = "monitor:job:stop")]
        [Log(Title = "停止任务", BusinessType = BusinessType.OTHER)]
        public async Task<IActionResult> Stop(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new CustomException("停止任务 Id 不能为空");
            }

            if (!_tasksQzService.Any(m => m.ID == id))
            {
                throw new CustomException("任务不存在");
            }

            var tasksQz = _tasksQzService.GetFirst(m => m.ID == id);
            var taskResult = await _schedulerServer.DeleteTaskScheduleAsync(tasksQz);//await _schedulerServer.PauseTaskScheduleAsync(tasksQz);

            if (taskResult.Code == 200)
            {
                tasksQz.IsStart = false;
                _tasksQzService.Update(tasksQz);
            }

            return ToResponse(taskResult);
        }

        /// <summary>
        /// 定时任务立即执行一次
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("run")]
        [ActionPermissionFilter(Permission = "monitor:job:run")]
        [Log(Title = "执行任务", BusinessType = BusinessType.OTHER)]
        public async Task<IActionResult> Run(string id)
        {
            if (!_tasksQzService.Any(m => m.ID == id))
            {
                throw new CustomException("任务不存在");
            }
            var tasksQz = _tasksQzService.GetFirst(m => m.ID == id);
            var taskResult = await _schedulerServer.RunTaskScheduleAsync(tasksQz);

            return ToResponse(taskResult);
        }

        /// <summary>
        /// 定时任务导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "定时任务导出")]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "monitor:job:export")]
        public IActionResult Export()
        {
            var list = _tasksQzService.GetAll();

            string sFileName = ExportExcel(list, "monitorjob", "定时任务");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }
    }
}
