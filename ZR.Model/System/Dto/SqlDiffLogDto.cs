using System.ComponentModel.DataAnnotations;
using MiniExcelLibs.Attributes;

namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 数据差异日志查询对象
    /// </summary>
    public class SqlDiffLogQueryDto : PagerInfo
    {
        public string TableName { get; set; }
        public string DiffType { get; set; }
        public string UserName { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
    }

    /// <summary>
    /// 数据差异日志输入输出对象
    /// </summary>
    public class SqlDiffLogDto
    {
        [Required(ErrorMessage = "主键不能为空")]
        [ExcelColumn(Name = "主键")]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long PId { get; set; }

        [ExcelColumn(Name = "表名")]
        public string TableName { get; set; }

        [ExcelColumn(Name = "业务数据内容")]
        public string BusinessData { get; set; }

        [ExcelColumn(Name = "差异类型")]
        public string DiffType { get; set; }

        [ExcelColumn(Name = "执行sql语句")]
        public string Sql { get; set; }

        [ExcelColumn(Name = "变更前数据")]
        public string BeforeData { get; set; }

        [ExcelColumn(Name = "变更后数据")]
        public string AfterData { get; set; }

        [ExcelColumn(Name = "操作用户名")]
        public string UserName { get; set; }

        [ExcelColumn(Name = "AddTime", Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? AddTime { get; set; }

        [ExcelColumn(Name = "数据库配置id")]
        public string ConfigId { get; set; }



    }
}