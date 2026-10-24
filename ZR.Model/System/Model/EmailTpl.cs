using ZR.Model.System;

namespace ZR.Model.Models
{
    /// <summary>
    /// 邮件发送模板。
    /// 系统级表（IMainDbEntity + [Tenant("0")]）：模板由平台统一维护，所有租户共用。
    /// </summary>
    [SugarTable("emailTpl")]
    [Tenant("0")]
    public class EmailTpl : SysBase, IMainDbEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        /// <summary>
        /// 模板名称（后台展示用）
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 业务编码（代码按 Code 取模板，如 workflow.notify / security.alert）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string Code { get; set; }
        /// <summary>
        /// 邮件主题模板，支持 {{key}} 占位符；为空时使用模板名称
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string Subject { get; set; }
        /// <summary>
        /// 模板内容，支持 {{key}} 占位符
        /// </summary>
        [SugarColumn(ColumnDescription = "模板内容", ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string Content { get; set; }
        /// <summary>
        /// 是否 HTML 模板：true 作为 HTML 正文发送，false 作为纯文本正文发送。
        /// 可空：存量库补列时新列为 NULL，按 true 处理。
        /// </summary>
        public bool? IsHtml { get; set; } = true;
        /// <summary>
        /// 是否启用：停用后按 Code 取模板会跳过，回退内置默认模板。
        /// 可空：存量库补列时新列为 NULL，按启用处理。
        /// </summary>
        public bool? Enabled { get; set; } = true;
    }
}
