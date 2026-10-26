using System;
using System.Collections.Generic;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 任务运行时快照：只包含 Quartz 调度器的实时状态。
    /// 执行次数、累计耗时等历史统计由调用方从 sys_tasks / sys_tasks_log 合并，避免这里耦合数据库查询。
    /// </summary>
    public class TaskRuntimeSnapshot
    {
        /// <summary>调度器是否已启动；未启动时 Tasks 为空</summary>
        public bool SchedulerStarted { get; set; }

        public List<TaskRuntimeInfo> Tasks { get; set; } = new List<TaskRuntimeInfo>();
    }

    /// <summary>单个任务的运行时状态</summary>
    public class TaskRuntimeInfo
    {
        /// <summary>任务ID（JobKey.Name）</summary>
        public string TaskId { get; set; }

        /// <summary>任务分组（JobKey.Group）</summary>
        public string JobGroup { get; set; }

        /// <summary>当前是否正在执行</summary>
        public bool IsRunning { get; set; }

        /// <summary>触发器是否全部处于暂停状态</summary>
        public bool Paused { get; set; }

        /// <summary>下次触发时间（本地时间），无后续触发时为 null</summary>
        public DateTime? NextFireTime { get; set; }

        /// <summary>上次触发时间（本地时间），从未触发时为 null</summary>
        public DateTime? PrevFireTime { get; set; }

        /// <summary>绑定的触发器数量</summary>
        public int TriggerCount { get; set; }
    }
}
