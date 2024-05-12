using MiniExcelLibs.Attributes;

namespace ZR.Model.System
{
    //[EpplusTable(PrintHeaders = true, AutofitColumns = true, AutoCalculate = true, ShowTotal = true)]
    public class SysBase
    {
        /// <summary>
        /// 创建人
        /// </summary>
        [SugarColumn(IsOnlyIgnoreUpdate = true, Length = 64, IsNullable = true)]
        [JsonProperty(propertyName: "CreateBy")]
        [ExcelIgnore]
        public string Create_by { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsOnlyIgnoreUpdate = true, IsNullable = true)]
        [JsonProperty(propertyName: "CreateTime")]
        [ExcelColumn(Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime Create_time { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新人
        /// </summary>
        [JsonIgnore]
        [JsonProperty(propertyName: "UpdateBy")]
        [SugarColumn(IsOnlyIgnoreInsert = true, Length = 64, IsNullable = true)]
        [ExcelIgnore]
        public string Update_by { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        //[JsonIgnore]
        [SugarColumn(IsOnlyIgnoreInsert = true, IsNullable = true)]
        [JsonProperty(propertyName: "UpdateTime")]
        [ExcelIgnore]
        public DateTime? Update_time { get; set; }
        [SugarColumn(Length = 500)]
        public string Remark { get; set; }
    }
}
