namespace ZR.Model.System.Dto
{
    public class TasksQueryDto : PagerInfo
    {
        /// <summary>
        /// 查询字符串
        /// </summary>
        [Display(Name = "查询字符串")]
        public string QueryText { get; set; }
        public int? TaskType { get; set; }
        public int? TriggerType { get; set; }
        public int? IsStart { get; set; }
    }

    /// <summary>
    /// 添加任务
    /// </summary>
    public class TasksCreateDto
    {
        /// <summary>
        /// 任务id
        /// </summary>
        [Display(Name = "任务id")]
        //[Required(ErrorMessage = "任务不能为空")]
        public string ID { get; set; }

        /// <summary>
        /// 任务名称
        /// </summary>
        [Display(Name = "任务名称")]
        [Required(ErrorMessage = "任务名称不能为空")]
        public string Name { get; set; }

        /// <summary>
        /// 任务分组
        /// </summary>
        [Display(Name = "任务分组")]
        [Required(ErrorMessage = "任务分组不能为空")]
        public string JobGroup { get; set; }

        /// <summary>
        /// 运行时间表达式
        /// </summary>
        [Display(Name = "运行时间表达式")]
        public string Cron { get; set; }

        /// <summary>
        /// 程序集名称
        /// </summary>
        [Display(Name = "程序集名称")]
        public string AssemblyName { get; set; }

        /// <summary>
        /// 任务所在类
        /// </summary>
        [Display(Name = "任务所在类")]
        public string ClassName { get; set; }

        /// <summary>
        /// 任务描述
        /// </summary>
        [Display(Name = "任务描述")]
        public string Remark { get; set; }

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
        /// </summary>
        [Display(Name = "触发器类型（0、simple 1、cron）")]
        public int TriggerType { get; set; }

        /// <summary>
        /// 执行间隔时间(单位:秒)
        /// </summary>
        [Display(Name = "执行间隔时间(单位:秒)")]
        public int IntervalSecond { get; set; }

        /// <summary>
        /// 传入参数
        /// </summary>
        [Display(Name = "传入参数")]
        public string JobParams { get; set; }
        public string ApiUrl { get; set; }
        /// <summary>
        /// 1、程序集任务 2、apiUrl任务 3、SQL语句
        /// </summary>
        public int TaskType { get; set; }
        /// <summary>
        /// SQL文本
        /// </summary>
        public string SqlText { get; set; }

        /// <summary>
        /// 网络请求方式
        /// </summary>
        public string RequestMethod { get; set; }
    }
}
