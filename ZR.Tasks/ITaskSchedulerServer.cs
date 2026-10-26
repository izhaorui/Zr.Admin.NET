using Infrastructure.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZR.Model.System;
using ZR.Tasks.TaskScheduler;

namespace ZR.Tasks
{
    public interface ITaskSchedulerServer
	{
		Task<ApiResult> StartTaskScheduleAsync();

		Task<ApiResult> StopTaskScheduleAsync();

		Task<ApiResult> AddTaskScheduleAsync(SysTasks tasksQz);

		Task<ApiResult> PauseTaskScheduleAsync(SysTasks tasksQz);

		Task<ApiResult> ResumeTaskScheduleAsync(SysTasks tasksQz);

		Task<ApiResult> DeleteTaskScheduleAsync(SysTasks tasksQz);

		Task<ApiResult> RunTaskScheduleAsync(SysTasks tasksQz, string operatorName);

		Task<ApiResult> UpdateTaskScheduleAsync(SysTasks tasksQz);

		/// <summary>
		/// 获取任务运行时快照：正在执行、下次/上次触发时间、暂停状态。
		/// 调度器未启动时返回 SchedulerStarted=false 且 Tasks 为空。
		/// </summary>
		/// <param name="taskIds">限定任务ID；为空表示全部</param>
		Task<TaskRuntimeSnapshot> GetRuntimeSnapshotAsync(IEnumerable<string> taskIds = null);
	}
}
