namespace ZR.Model.Models
{
    /// <summary>
    /// 邮件发送记录
    /// </summary>
    [SugarTable("email_log")]
    [Tenant("0")]
    public class EmailLog
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
        /// 接收邮箱
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string ToEmails{ get; set; }
        /// <summary>
        /// 邮件内容
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string EmailContent { get; set; }
        /// <summary>
        /// 发送时间
        /// </summary>
        public DateTime AddTime { get; set; } = DateTime.Now;
        /// <summary>
        /// 是否已发送
        /// </summary>
        public int IsSend { get; set; }
        /// <summary>
        /// 发送结果
        /// </summary>
        public string SendResult { get; set; }
        /// <summary>
        /// 附件地址
        /// </summary>
        public string FileUrl { get; set; }
        /// <summary>
        /// 发送时间
        /// </summary>
        public DateTime SendTime { get; set; }
    }
}
