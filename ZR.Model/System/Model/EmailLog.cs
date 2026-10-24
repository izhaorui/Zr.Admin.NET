namespace ZR.Model.Models
{
    /// <summary>
    /// 邮件发送记录。
    /// 系统级表（IMainDbEntity + [Tenant("0")]）：始终落系统库，后台队列按系统库扫描与重试，
    /// 避免多租户下日志分散到各租户库导致重试任务找不到记录。
    /// </summary>
    [SugarTable("email_log")]
    [Tenant("0")]
    public class EmailLog : IMainDbEntity
    {
        [JsonConverter(typeof(ValueToStringConverter))]
        [SugarColumn(IsPrimaryKey = true, IsIdentity = false)]
        public long Id { get; set; }
        /// <summary>
        /// 发送人名称
        /// </summary>
        public string FromName { get; set; }
        /// <summary>
        /// 发送邮箱
        /// </summary>
        public string FromEmail { get; set; }
        /// <summary>
        /// 邮件主题
        /// </summary>
        public string Subject { get; set; }
        /// <summary>
        /// 接收邮箱（多个用逗号分隔）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string ToEmails{ get; set; }
        /// <summary>
        /// 抄送邮箱（多个用逗号分隔）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string CcEmails { get; set; }
        /// <summary>
        /// 密送邮箱（多个用逗号分隔）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string BccEmails { get; set; }
        /// <summary>
        /// 邮件内容（优先存 HTML 正文）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string EmailContent { get; set; }
        /// <summary>
        /// 记录创建时间
        /// </summary>
        public DateTime AddTime { get; set; } = DateTime.Now;
        /// <summary>
        /// 是否已发送（历史字段，等价于 Status == 1；新代码请用 Status）
        /// </summary>
        public int IsSend { get; set; }
        /// <summary>
        /// 发送状态：0待发送 1发送成功 2发送失败 3发送中 4草稿（见 EmailSendStatus）。
        /// 可空：存量库补列时新列是 NULL，历史记录归为「未知/待发送」，由查询层归一展示。
        /// </summary>
        public int? Status { get; set; }
        /// <summary>
        /// 已重试次数（存量记录为 NULL，按 0 处理）
        /// </summary>
        public int? RetryCount { get; set; }
        /// <summary>
        /// 下次重试时间（失败后按指数退避计算；成功或草稿为空）
        /// </summary>
        public DateTime? NextRetryTime { get; set; }
        /// <summary>
        /// 最近一次开始发送的时间。用于识别进程异常退出后停留在「发送中」的记录，
        /// 避免用 AddTime 判断时把积压队列里正在投递的老记录误判为僵死。
        /// </summary>
        public DateTime? LastAttemptTime { get; set; }
        /// <summary>
        /// 失败错误码（NO_CONFIG/TIMEOUT/SMTP_xxx 等）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string ErrorCode { get; set; }
        /// <summary>
        /// 邮件 Message-Id（成功发送后回填，便于与服务端日志对账）
        /// </summary>
        [SugarColumn(Length = 128, IsNullable = true)]
        public string MessageId { get; set; }
        /// <summary>
        /// 业务类型：manual 手动发送 / template 模板发送 / workflow 工作流通知 / security 安全提醒
        /// </summary>
        [SugarColumn(Length = 32, IsNullable = true)]
        public string EmailType { get; set; }
        /// <summary>
        /// 投递方式：0 同步直发（不自动重发） / 1 后台队列（可恢复、可重试）。
        /// 存量记录为 NULL，按同步直发处理，不参与启动恢复。
        /// </summary>
        public int? DispatchMode { get; set; }
        /// <summary>
        /// 发送结果（成功 ok / simulated，失败为 [错误码]错误信息）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string SendResult { get; set; }
        /// <summary>
        /// 附件地址（本地绝对路径，多个用逗号分隔；仅用于重发恢复）
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
        public string FileUrl { get; set; }
        /// <summary>
        /// 发送成功时间。
        /// 保持非空类型：存量库该列是 NOT NULL，CodeFirst 不会把列改成可空；
        /// 未发送时统一写 <see cref="NoSendTime"/> 哨兵值（与 SysTwoFactorService.NoTrustUntil 同一处理方式）。
        /// </summary>
        public DateTime SendTime { get; set; } = NoSendTime;

        /// <summary>「尚未发送」哨兵值：SQL Server datetime 下限，且恒早于当前时间，便于条件判断。</summary>
        public static readonly DateTime NoSendTime = new DateTime(1900, 1, 1);
    }
}
