namespace ZR.Model.System
{
    /// <summary>
    /// 任务日志
    /// </summary>
    [SugarTable("sys_tasks_log", "任务日志表")]
    [Tenant("0")]
    public class SysTasksLog
    {
        /// <summary>
        /// 日志Id
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long JobLogId { get; set; }
        /// <summary>
        /// 任务Id
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string JobId { get; set; }
        /// <summary>
        /// 任务名
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string JobName { get; set; }
        /// <summary>
        /// 任务分组
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string JobGroup { get; set; }
        /// <summary>
        /// 执行状态（0正常 1失败）
        /// </summary>
        [SugarColumn(DefaultValue = "0", Length = 20)]
        public string Status { get; set; }
        /// <summary>
        /// 异常
        /// </summary>
        [SugarColumn(Length = 5000)]
        public string Exception { get; set; }
        /// <summary>
        /// 任务消息
        /// </summary>
        public string JobMessage { get; set; }
        /// <summary>
        /// 调用目标字符串
        /// </summary>
        public string InvokeTarget { get; set; }

        /// <summary>
        /// 所属租户
        /// </summary>
        [SugarColumn(Length = 64)]
        public string TenantId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }
        /// <summary>
        /// 执行用时，毫秒
        /// </summary>
        //[SqlSugar.SugarColumn(IsIgnore = true)]
        public double Elapsed { get; set; }

        /// <summary>
        /// 执行机器名称
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// 操作人
        /// </summary>
        [SugarColumn(Length = 100)]
        public string Operator { get; set; }

        /// <summary>
        /// 链路追踪id
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 是否为手动触发（0-系统触发/定时；1-用户手动触发）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsManual { get; set; }

        /// <summary>
        /// 触发来源（cron/manual/api/retry）
        /// </summary>
        [SugarColumn(Length = 50)]
        public string TriggerSource { get; set; }
    }
}
