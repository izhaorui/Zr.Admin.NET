using System;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Model
{
    public class SendEmailDto
    {
        /// <summary>
        /// 文件地址
        /// </summary>
        public string FileUrl { get; set; } = "";
        /// <summary>
        /// 主题
        /// </summary>
        [Required(ErrorMessage = "主题不能为空")]
        public string Subject { get; set; }
        [Required(ErrorMessage = "发送人不能为空")]
        public string ToUser { get; set; }
        public string Content { get; set; } = "";
        public string HtmlContent { get; set; }
        /// <summary>
        /// 是否发送给自己
        /// </summary>
        public bool SendMe { get; set; }
        public DateTime AddTime { get; set; }
        /// <summary>
        /// 是否发送
        /// </summary>
        public bool IsSend { get; set; }
        /// <summary>
        /// 发送邮箱
        /// </summary>
        public string FromName { get; set; }
        public string FromEmail { get; set; }
    }
}
