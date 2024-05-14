namespace ZR.Model.System
{
    ///<summary>
    ///计划任务
    ///</summary>
    [SugarTable("sys_tasks", "计划任务表")]
    [Tenant("0")]
    public class SysTasks : SysBase
    {
        public SysTasks()
        {
        }

        /// <summary>
        /// 任务id
        /// </summary>
        [Display(Name = "任务id")]
        //[JsonConverter(typeof(ValueToStringConverter))]
        [SugarColumn(IsPrimaryKey = true)]
        public string ID { get; set; }

        /// <summary>
        /// 任务名称 
        /// </summary>
        [Display(Name = "任务名称")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Name { get; set; }

        /// <summary>
        /// 任务分组 
        /// </summary>
        [Display(Name = "任务分组")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string JobGroup { get; set; }

        /// <summary>
        /// 运行时间表达式 
        /// </summary>
        [Display(Name = "运行时间表达式")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Cron { get; set; }

        /// <summary>
        /// 程序集名称 
        /// </summary>
        [Display(Name = "程序集名称")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string AssemblyName { get; set; }

        /// <summary>
        /// 任务所在类 
        /// </summary>
        [Display(Name = "任务所在类")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string ClassName { get; set; }

        /// <summary>
        /// 执行次数 
        /// </summary>
        [Display(Name = "执行次数")]
        [SugarColumn(DefaultValue = "0", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public int RunTimes { get; set; }

        /// <summary>
        /// 开始时间 
        /// </summary>
        [Display(Name = "开始时间")]
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// 结束时间 
        /// </summary>
        [Display(Name = "结束时间")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 触发器类型（0、simple 1、cron） 
        /// 默认 : 1
        /// </summary>
        [Display(Name = "触发器类型（0、simple 1、cron）")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public int TriggerType { get; set; }

        /// <summary>
        /// 执行间隔时间(单位:秒) 
        /// 默认 : 0
        /// </summary>
        [Display(Name = "执行间隔时间(单位:秒)")]
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public int IntervalSecond { get; set; }

        /// <summary>
        /// 是否启动
        /// </summary>
        [Display(Name = "是否启动")]
        [SugarColumn(DefaultValue = "0", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public int IsStart { get; set; }

        /// <summary>
        /// 传入参数
        /// </summary>
        [Display(Name = "传入参数")]
        public string JobParams { get; set; }

        /// <summary>
        /// 最后运行时间
        /// </summary>
        public DateTime? LastRunTime { get; set; }
        /// <summary>
        /// api执行地址
        /// </summary>
        public string ApiUrl { get; set; }
        /// <summary>
        /// 任务类型 1、程序集 2、网络请求 3、SQL语句
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int TaskType { get; set; }

        /// <summary>
        /// SQL语句
        /// </summary>
        public string SqlText { get; set; }
        /// <summary>
        /// 网络请求方式
        /// </summary>
        [SugarColumn(Length = 20)]
        public string RequestMethod { get; set; }
    }
}
