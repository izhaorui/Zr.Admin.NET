namespace ZR.Model.System
{
    ///<summary>
    ///计划任务
    ///</summary>
    [SugarTable("sys_tasks", "计划任务表")]
    [Tenant("0")]
    public class SysTasks : SysBase, IMainDbEntity
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
        [SugarColumn(Length = 200, ExtendedAttribute = ProteryConstant.NOTNULL)]
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
        /// 错过执行（Misfire）策略：
        /// 0=默认（Cron 不补执行 / Simple 跳到下一个计划点）
        /// 1=立即补执行一次
        /// 2=忽略错过，按原计划全部补执行
        /// 注意：Cron 与 Simple 的 Quartz 指令数值语义不同，存储统一用本业务枚举，由触发器创建时分别映射。
        /// </summary>
        [Display(Name = "错过执行策略")]
        [SugarColumn(DefaultValue = "0")]
        public int MisfirePolicy { get; set; }

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
        /// 最后成功时间
        /// </summary>
        public DateTime? LastSuccessTime { get; set; }

        /// <summary>
        /// 最后失时间
        /// </summary>
        public DateTime? LastFailTime { get; set; }

        /// <summary>
        /// 最后运行状态 0、成功 1、失败
        /// </summary>
        [SugarColumn(Length = 10)]
        public string LastRunStatus { get; set; }

        /// <summary>
        /// 最后运行错误信息
        /// </summary>
        [SugarColumn(Length = 4000)]
        public string LastErrorMsg { get; set; }

        /// <summary>
        /// 所属租户
        /// </summary>
        [Display(Name = "所属租户")]
        [SugarColumn(Length = 64, IsNullable = true)]
        public string TenantId { get; set; }

        /// <summary>
        /// api执行地址
        /// </summary>
        public string ApiUrl { get; set; }
        /// <summary>
        /// 任务类型 1、程序集 2、网络请求 3、存储过程
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int TaskType { get; set; }

        /// <summary>
        /// 存储过程名称（schema.ProcedureName）；为兼容历史数据沿用 SqlText 字段
        /// </summary>
        public string SqlText { get; set; }
        /// <summary>
        /// 网络请求方式
        /// </summary>
        [SugarColumn(Length = 20)]
        public string RequestMethod { get; set; }
    }
}
