using MiniExcelLibs.Attributes;
using Newtonsoft.Json;
using SqlSugar;
using System;

namespace ZR.Model.System
{
    //[EpplusTable(PrintHeaders = true, AutofitColumns = true, AutoCalculate = true, ShowTotal = true)]
    public class SysBase
    {
        [SugarColumn(IsOnlyIgnoreUpdate = true)]
        [JsonProperty(propertyName: "CreateBy")]
        [ExcelIgnore]
        public string Create_by { get; set; }

        [SugarColumn(IsOnlyIgnoreUpdate = true)]
        [JsonProperty(propertyName: "CreateTime")]
        [ExcelColumn(Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime Create_time { get; set; } = DateTime.Now;

        [JsonIgnore]
        [JsonProperty(propertyName: "UpdateBy")]
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        [ExcelIgnore]
        public string Update_by { get; set; }

        //[JsonIgnore]
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        [JsonProperty(propertyName: "UpdateTime")]
        [ExcelIgnore]
        public DateTime? Update_time { get; set; }
        public string Remark { get; set; }
        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        [ExcelIgnore]
        public DateTime? BeginTime { get; set; }
        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        [ExcelIgnore]
        public DateTime? EndTime { get; set; }
    }
}
