using ZR.Model.System;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 规格模板
    /// </summary>
    [SugarTable("mms_spec_template")]
    [Tenant("MallDb")]
    public class SpecTemplate : SysBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(Length = 100, ColumnDescription = "模板名称")]
        public string TemplateName { get; set; }

        [SugarColumn(IsJson = true, ColumnDescription = "规格结构")]
        public List<SpecDto> SpecJson { get; set; }
    }
    public class SpecDto
    {
        public string Name { get; set; }
        public List<string> Values { get; set; }
    }
}
