using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ZR.Model.System.Dto
{
    public class TasksQueryDto
    {
        /// <summary>
        /// 描述 : 查询字符串 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "查询字符串")]
        public string QueryText { get; set; }
    }

    /// <summary>
    /// 添加任务
    /// </summary>
    public class TasksCreateDto
    {
        /// <summary>
        /// 描述 : 任务名称 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务名称")]
        [Required(ErrorMessage = "任务名称不能为空")]
        public string Name { get; set; }

        /// <summary>
        /// 描述 : 任务分组 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务分组")]
        [Required(ErrorMessage = "任务分组不能为空")]
        public string JobGroup { get; set; }

        /// <summary>
        /// 描述 : 运行时间表达式 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "运行时间表达式")]
        public string Cron { get; set; }

        /// <summary>
        /// 描述 : 程序集名称 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "程序集名称")]
        //[Required(ErrorMessage = "程序集名称不能为空")]
        public string AssemblyName { get; set; }

        /// <summary>
        /// 描述 : 任务所在类 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务所在类")]
        //[Required(ErrorMessage = "任务所在类不能为空")]
        public string ClassName { get; set; }

        /// <summary>
        /// 描述 : 任务描述 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务描述")]
        public string Remark { get; set; }

        /// <summary>
        /// 描述 : 开始时间 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "开始时间")]
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// 描述 : 结束时间 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "结束时间")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 描述 : 触发器类型（0、simple 1、cron） 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "触发器类型（0、simple 1、cron）")]
        public int TriggerType { get; set; }

        /// <summary>
        /// 描述 : 执行间隔时间(单位:秒) 
        /// 空值 : False
        /// 默认 : 0
        /// </summary>
        [Display(Name = "执行间隔时间(单位:秒)")]
        public int IntervalSecond { get; set; }

        /// <summary>
        /// 描述 : 传入参数 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "传入参数")]
        public string JobParams { get; set; }
        public string ApiUrl { get; set; }
        /// <summary>
        /// 1、程序集任务 2、apiUrl任务
        /// </summary>
        public int TaskType { get; set; }
    }

    /// <summary>
    /// 更新任务
    /// </summary>
    public class TasksUpdateDto
    {
        /// <summary>
        /// 描述 : UID 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "UID")]
        [Required(ErrorMessage = "UID不能为空")]
        public string ID { get; set; }

        /// <summary>
        /// 描述 : 任务名称 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务名称")]
        [Required(ErrorMessage = "任务名称不能为空")]
        public string Name { get; set; }

        /// <summary>
        /// 描述 : 任务分组 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务分组")]
        [Required(ErrorMessage = "任务分组不能为空")]
        public string JobGroup { get; set; }

        /// <summary>
        /// 描述 : 运行时间表达式 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "运行时间表达式")]
        public string Cron { get; set; }

        /// <summary>
        /// 描述 : 程序集名称 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "程序集名称")]
        [Required(ErrorMessage = "程序集名称不能为空")]
        public string AssemblyName { get; set; }

        /// <summary>
        /// 描述 : 任务所在类 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务所在类")]
        [Required(ErrorMessage = "任务所在类不能为空")]
        public string ClassName { get; set; }

        /// <summary>
        /// 描述 : 任务描述 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务描述")]
        public string Remark { get; set; }

        /// <summary>
        /// 描述 : 开始时间 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "开始时间")]
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// 描述 : 结束时间 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "结束时间")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 描述 : 触发器类型（0、simple 1、cron） 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "触发器类型（0、simple 1、cron）")]
        public int TriggerType { get; set; }

        /// <summary>
        /// 描述 : 执行间隔时间(单位:秒) 
        /// 空值 : False
        /// 默认 : 0
        /// </summary>
        [Display(Name = "执行间隔时间(单位:秒)")]
        public int IntervalSecond { get; set; }

        /// <summary>
        /// 描述 : 传入参数 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "传入参数")]
        public string JobParams { get; set; }
        public string ApiUrl { get; set; }
        /// <summary>
        /// 1、程序集任务 2、apiUrl任务
        /// </summary>
        public int TaskType { get; set; }
    }
}
