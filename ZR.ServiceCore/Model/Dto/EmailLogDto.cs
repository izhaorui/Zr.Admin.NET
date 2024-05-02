namespace ZR.Model.Dto
{
    /// <summary>
    /// 邮件发送记录查询对象
    /// </summary>
    public class EmailLogQueryDto : PagerInfo
    {
        public string FromEmail { get; set; }
        public string Subject { get; set; }
        public int? IsSend { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
    }

    /// <summary>
    /// 邮件发送记录输入输出对象
    /// </summary>
    public class EmailLogDto
    {
        //[Required(ErrorMessage = "Id不能为空")]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long Id { get; set; }

        public string FromEmail { get; set; }

        public string Subject { get; set; }

        public string ToEmails { get; set; }

        public string EmailContent { get; set; }

        public DateTime? AddTime { get; set; }
        /// <summary>
        /// 是否已发送
        /// </summary>
        public int IsSend { get; set; }
        /// <summary>
        /// 发送结果
        /// </summary>
        public string SendResult { get; set; }
        public long[] IdArr { get; set; }
        public string FromName { get; set; }
    }
}