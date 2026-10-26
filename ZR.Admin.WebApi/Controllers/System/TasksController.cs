using Microsoft.AspNetCore.Mvc;
using Quartz;
using SqlSugar;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.ServiceCore.AI.IService;
using ZR.Tasks;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 计划任务
    /// </summary>
    [Route("system/Tasks")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class TasksController : BaseController
    {
        private ISysTasksQzService _tasksQzService;
        private ITaskSchedulerServer _schedulerServer;
        private readonly ISysTenantService _sysTenantService;
        private readonly ISysAiService _sysAiService;
        private readonly ISysTasksLogService _tasksLogService;

        public TasksController(
            ISysTasksQzService sysTasksQzService,
            ITaskSchedulerServer taskScheduler,
            ISysTenantService sysTenantService,
            ISysAiService sysAiService,
            ISysTasksLogService tasksLogService)
        {
            _tasksQzService = sysTasksQzService;
            _schedulerServer = taskScheduler;
            _sysTenantService = sysTenantService;
            _sysAiService = sysAiService;
            _tasksLogService = tasksLogService;
        }

        /// <summary>
        /// 当前登录者是否可访问该任务（管理员放行；否则按租户精确匹配 / 通配 * / 逗号列表判断）。
        /// 属于 Web 层鉴权（依赖 HttpContext），因此保留在 Controller，不下沉到服务层。
        /// </summary>
        private bool CanAccess(SysTasks task)
        {
            if (HttpContext.IsAdmin())
            {
                return true;
            }

            var currentTenantId = App.GetCurrentTenantId();

            if (string.Equals(task.TenantId, currentTenantId, StringComparison.OrdinalIgnoreCase)
                || task.TenantId == "*")
            {
                return true;
            }

            if (task.TenantId != null)
            {
                var parts = task.TenantId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Contains(currentTenantId, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureTaskAccess(SysTasks task)
        {
            if (task == null)
            {
                throw new CustomException("任务不存在");
            }

            if (!CanAccess(task))
            {
                throw new CustomException("无权访问当前租户任务");
            }
        }

        private SysTasks GetTaskById(string id)
        {
            var task = _tasksQzService.GetFirst(m => m.ID == id);
            EnsureTaskAccess(task);
            return task;
        }

        /// <summary>
        /// 查询计划任务列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "monitor:job:list")]
        public IActionResult ListTask([FromQuery] TasksQueryDto parm)
        {
            var response = _tasksQzService.SelectTaskList(parm);

            var totalRuns = response.Result.Sum(x => x.RunTimes);
            var successRuns = response.Result.Count(x => x.LastRunStatus == "0");
            var failureRuns = response.Result.Count(x => x.LastRunStatus == "1");
            var totalTasks = response.Result.Count();
            response.Extra.Add("totalRuns", totalRuns);
            response.Extra.Add("successRuns", successRuns);
            response.Extra.Add("failureRuns", failureRuns);
            response.Extra.Add("successRate", totalTasks > 0 ? Math.Round((double)successRuns / totalTasks * 100, 2) : 0);
            response.Extra.Add("failureRate", totalTasks > 0 ? Math.Round((double)failureRuns / totalTasks * 100, 2) : 0);
            return SUCCESS(response, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 任务运行监控：正在执行、下次/上次触发时间、累计耗时。
        /// 运行时状态取自 Quartz 内存，执行次数与累计耗时来自数据库，按任务ID合并；
        /// 只返回当前仍在调度器中的任务（已停止的任务不在其中）。
        /// </summary>
        /// <param name="ids">可选，逗号分隔的任务ID；为空表示全部</param>
        [HttpGet("runtime")]
        [ActionPermissionFilter(Permission = "monitor:job:list")]
        public async Task<IActionResult> Runtime([FromQuery] string ids = null)
        {
            var idList = string.IsNullOrWhiteSpace(ids)
                ? null
                : ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var snapshot = await _schedulerServer.GetRuntimeSnapshotAsync(idList);

            var taskIds = snapshot.Tasks.Select(t => t.TaskId).ToList();
            var taskMap = taskIds.Count == 0
                ? new Dictionary<string, SysTasks>()
                : _tasksQzService.GetList(m => taskIds.Contains(m.ID))
                    .Where(CanAccess)
                    .ToDictionary(m => m.ID);

            var elapsed = await _tasksLogService.GetElapsedSummaryAsync(taskMap.Keys);

            var tasks = snapshot.Tasks
                .Where(t => taskMap.ContainsKey(t.TaskId))
                .Select(t =>
                {
                    var task = taskMap[t.TaskId];
                    return new
                    {
                        taskId = t.TaskId,
                        jobGroup = t.JobGroup,
                        name = task.Name,
                        taskType = task.TaskType,
                        isRunning = t.IsRunning,
                        paused = t.Paused,
                        nextFireTime = t.NextFireTime,
                        prevFireTime = t.PrevFireTime,
                        triggerCount = t.TriggerCount,
                        runTimes = task.RunTimes,
                        lastRunStatus = task.LastRunStatus,
                        lastRunTime = task.LastRunTime,
                        totalElapsedMs = elapsed.TryGetValue(t.TaskId, out var e) ? Math.Round(e, 0) : 0
                    };
                })
                .ToList();

            return SUCCESS(new
            {
                schedulerStarted = snapshot.SchedulerStarted,
                tasks
            });
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
                return SUCCESS(GetTaskById(id));
            }
            return SUCCESS(0);
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
            var tenantExpression = _tasksQzService.NormalizeTenantExpression(parm.TenantId, _sysTenantService);

            //判断是否已经存在
            if (_tasksQzService.Any(m => m.TenantId == tenantExpression && m.Name == parm.Name))
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
            if (string.IsNullOrEmpty(parm.SqlText) && parm.TaskType == 3)
            {
                throw new CustomException($"sql语句不能为空");
            }
            if (parm.TaskType == 1 && (parm.AssemblyName.IsEmpty() || parm.ClassName.IsEmpty()))
            {
                throw new CustomException($"程序集或者类名不能为空");
            }
            //从 Dto 映射到 实体
            var tasksQz = parm.Adapt<SysTasks>().ToCreate(HttpContext);
            tasksQz.Create_by = HttpContext.GetName();
            tasksQz.ID = SnowFlakeSingle.Instance.NextId().ToString();
            tasksQz.TenantId = tenantExpression;

            return SUCCESS(_tasksQzService.AddTasks(tasksQz));
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <returns></returns>
        [HttpPost("update")]
        [ActionPermissionFilter(Permission = "monitor:job:edit")]
        [Log(Title = "修改任务", BusinessType = BusinessType.UPDATE)]
        public IActionResult Update([FromBody] TasksCreateDto parm)
        {
            var tenantExpression = _tasksQzService.NormalizeTenantExpression(parm.TenantId, _sysTenantService);

            //判断是否已经存在
            if (_tasksQzService.Any(m => m.TenantId == tenantExpression && m.Name == parm.Name && m.ID != parm.ID))
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
            var tasksQz = GetTaskById(parm.ID);
            if (string.IsNullOrEmpty(parm.ApiUrl) && parm.TaskType == 2)
            {
                throw new CustomException($"api地址不能为空");
            }

            if (tasksQz.IsStart == 1)
            {
                throw new CustomException($"该任务正在运行中，请先停止在更新");
            }
            var model = parm.Adapt<SysTasks>();
            model.TenantId = tenantExpression;
            model.Update_by = HttpContextExtension.GetName(HttpContext);
            int response = _tasksQzService.UpdateTasks(model);

            // 说明：更新仅允许在任务已停止（IsStart==0）时进行（见上方校验），
            // 此时任务已从 Quartz 中删除，无需重建调度；若此处调用 UpdateTaskScheduleAsync，
            // 反而会把已停止的任务重新注册并跑起来。任务启动统一由 start 接口负责。
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

            var tasksQz = GetTaskById(id);
            var taskResult = await _schedulerServer.DeleteTaskScheduleAsync(tasksQz);

            if (taskResult.IsSuccess())
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

            var tasksQz = GetTaskById(id);
            var taskResult = await _schedulerServer.AddTaskScheduleAsync(tasksQz);

            if (taskResult.IsSuccess())
            {
                tasksQz.IsStart = 1;
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

            var tasksQz = GetTaskById(id);
            var taskResult = await _schedulerServer.DeleteTaskScheduleAsync(tasksQz);//await _schedulerServer.PauseTaskScheduleAsync(tasksQz);

            if (taskResult.IsSuccess())
            {
                tasksQz.IsStart = 0;
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
            var tasksQz = GetTaskById(id);
            var userName = HttpContext.GetName();
            var taskResult = await _schedulerServer.RunTaskScheduleAsync(tasksQz, userName);

            return ToResponse(taskResult);
        }

        /// <summary>
        /// AI 自然语言调度描述转 Cron 表达式。AI 只负责生成，表达式合法性与执行时间由服务端用 Quartz 复核。
        /// </summary>
        [HttpPost("ai/cron")]
        [ActionPermissionFilter(Permission = "monitor:job:ai")]
        public async Task<IActionResult> AiCron([FromBody] SysAiCronParseInput parm)
        {
            try
            {
                var result = await _sysAiService.ParseCronAsync(parm);
                if (!string.IsNullOrWhiteSpace(result.Cron))
                {
                    if (!CronExpression.IsValidExpression(result.Cron))
                    {
                        return ToResponse(ResultCode.FAIL, $"AI 生成的 cron 表达式不合法（{result.Cron}），请调整描述后重试");
                    }
                    result.NextTimes = GetNextFireTimes(result.Cron, 3);
                }
                return SUCCESS(result);
            }
            catch (Exception ex)
            {
                return ToResponse(ResultCode.FAIL, ex.Message);
            }
        }

        /// <summary>
        /// 按 Quartz 计算接下来若干次执行时间，便于用户核对 AI 生成的表达式是否符合预期。
        /// </summary>
        private static List<string> GetNextFireTimes(string cron, int count)
        {
            var times = new List<string>();
            var expr = new CronExpression(cron);
            var cursor = DateTimeOffset.Now;

            for (var i = 0; i < count; i++)
            {
                var next = expr.GetNextValidTimeAfter(cursor);
                if (next == null) break;

                times.Add(next.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                cursor = next.Value;
            }
            return times;
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
