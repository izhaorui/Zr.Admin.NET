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
        /// <summary>发送状态：0待发送 1成功 2失败 3发送中 4草稿</summary>
        public int? Status { get; set; }
        /// <summary>业务类型：manual/template/workflow/security</summary>
        public string EmailType { get; set; }
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

        public string CcEmails { get; set; }

        public string BccEmails { get; set; }

        public string EmailContent { get; set; }

        public DateTime? AddTime { get; set; }
        /// <summary>
        /// 是否已发送（历史字段）
        /// </summary>
        public int IsSend { get; set; }
        /// <summary>
        /// 发送状态：0待发送 1成功 2失败 3发送中 4草稿
        /// </summary>
        public int? Status { get; set; }
        /// <summary>
        /// 已重试次数
        /// </summary>
        public int? RetryCount { get; set; }
        /// <summary>
        /// 下次重试时间
        /// </summary>
        public DateTime? NextRetryTime { get; set; }
        /// <summary>
        /// 最近一次开始发送的时间
        /// </summary>
        public DateTime? LastAttemptTime { get; set; }
        /// <summary>
        /// 失败错误码
        /// </summary>
        public string ErrorCode { get; set; }
        /// <summary>
        /// 邮件 Message-Id
        /// </summary>
        public string MessageId { get; set; }
        /// <summary>
        /// 业务类型
        /// </summary>
        public string EmailType { get; set; }
        /// <summary>
        /// 发送结果
        /// </summary>
        public string SendResult { get; set; }
        /// <summary>
        /// 附件地址
        /// </summary>
        public string FileUrl { get; set; }
        /// <summary>
        /// 发送成功时间
        /// </summary>
        public DateTime? SendTime { get; set; }
        public long[] IdArr { get; set; }
        public string FromName { get; set; }
    }
}
