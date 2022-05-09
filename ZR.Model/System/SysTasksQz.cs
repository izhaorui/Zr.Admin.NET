using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ZR.Model.System
{
    ///<summary>
    ///计划任务
    ///</summary>
    [SugarTable("sys_tasks")]
    [Tenant("0")]
    public class SysTasksQz
    {
        public SysTasksQz()
        {
        }

        /// <summary>
        /// 描述 : UID 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "UID")]
        [SugarColumn(IsPrimaryKey = true)]
        public string ID { get; set; }

        /// <summary>
        /// 描述 : 任务名称 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务名称")]
        public string Name { get; set; }

        /// <summary>
        /// 描述 : 任务分组 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务分组")]
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
        public string AssemblyName { get; set; }

        /// <summary>
        /// 描述 : 任务所在类 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务所在类")]
        public string ClassName { get; set; }

        /// <summary>
        /// 描述 : 任务描述 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "任务描述")]
        public string Remark { get; set; }

        /// <summary>
        /// 描述 : 执行次数 
        /// 空值 : False
        /// 默认 : 0
        /// </summary>
        [Display(Name = "执行次数")]
        public int RunTimes { get; set; }

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
        /// 默认 : 1
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
        /// 描述 : 是否启动 
        /// 空值 : False
        /// 默认 : 0
        /// </summary>
        [Display(Name = "是否启动")]
        public bool IsStart { get; set; }

        /// <summary>
        /// 描述 : 传入参数 
        /// 空值 : True
        /// 默认 : 
        /// </summary>
        [Display(Name = "传入参数")]
        public string JobParams { get; set; }

        [SugarColumn(IsOnlyIgnoreUpdate = true)]//设置后修改不会有此字段
        [JsonProperty(propertyName: "CreateBy")]
        public string Create_by { get; set; }

        /// <summary>
        /// 描述 : 创建时间 
        /// 空值 : False
        /// 默认 : 
        /// </summary>
        //[Display(Name = "创建时间")]
        [SugarColumn(IsOnlyIgnoreUpdate = true)]//设置后修改不会有此字段
        [JsonProperty(propertyName: "CreateTime")]
        public DateTime Create_time { get; set; } = DateTime.Now;

        [JsonIgnore]
        [JsonProperty(propertyName: "UpdateBy")]
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        public string Update_by { get; set; }

        [SugarColumn(IsOnlyIgnoreInsert = true)]//设置后插入数据不会有此字段
        [JsonProperty(propertyName: "UpdateTime")]
        public DateTime Update_time { get; set; } = DateTime.Now;
        /// <summary>
        /// 最后运行时间
        /// </summary>
        public DateTime? LastRunTime { get; set; }
        /// <summary>
        /// api执行地址
        /// </summary>
        public string ApiUrl { get; set; }
        /// <summary>
        /// 任务类型 1程序集2网络请求
        /// </summary>
        public int TaskType { get; set; }
    }
}
