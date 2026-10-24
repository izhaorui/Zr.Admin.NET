namespace ZR.Model.Dto
{
    /// <summary>
    /// 邮件模板查询对象
    /// </summary>
    public class EmailTplQueryDto : PagerInfo 
    {
        public string Name { get; set; }

        /// <summary>业务编码</summary>
        public string Code { get; set; }

        /// <summary>是否启用</summary>
        public bool? Enabled { get; set; }
    }

    /// <summary>
    /// 邮件模板输入输出对象
    /// </summary>
    public class EmailTplDto
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Name不能为空")]
        public string Name { get; set; }

        /// <summary>业务编码（代码按 Code 取模板，如 workflow.notify）</summary>
        [StringLength(64, ErrorMessage = "编码长度不能超过64")]
        public string Code { get; set; }

        /// <summary>邮件主题模板，支持 {{key}} 占位符</summary>
        [StringLength(200, ErrorMessage = "主题长度不能超过200")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "模板内容不能为空")]
        public string Content { get; set; }

        /// <summary>是否 HTML 模板</summary>
        public bool IsHtml { get; set; } = true;

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; } = true;

        public string CreateBy { get; set; }

        public DateTime? CreateTime { get; set; }

        public string UpdateBy { get; set; }

        public DateTime? UpdateTime { get; set; }

        public string Remark { get; set; }



    }
}
