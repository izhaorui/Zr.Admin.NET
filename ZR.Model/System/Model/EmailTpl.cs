using ZR.Model.System;

namespace ZR.Model.Models
{
    /// <summary>
    /// 邮件发送模板
    /// </summary>
    [SugarTable("emailTpl")]
    [Tenant("0")]
    public class EmailTpl : SysBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public string Name { get; set; }
        [SugarColumn(ColumnDescription = "模板内容", ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string Content { get; set; }
    }
}
