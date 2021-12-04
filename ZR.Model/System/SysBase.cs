//using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using SqlSugar;
using OfficeOpenXml.Attributes;

namespace ZR.Model.System
{
    [EpplusTable(PrintHeaders = true, AutofitColumns = true, AutoCalculate = true, ShowTotal = true)]
    public class SysBase
    {
        [SugarColumn(IsOnlyIgnoreUpdate = true)]//设置后修改不会有此字段
        [JsonProperty(propertyName: "CreateBy")]
        [EpplusIgnore]
        public string Create_by { get; set; }

        [SugarColumn(IsOnlyIgnoreUpdate = true)]//设置后修改不会有此字段
        [JsonProperty(propertyName: "CreateTime")]
        [EpplusTableColumn(NumberFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime Create_time { get; set; } = DateTime.Now;

        [JsonIgnore]
        [JsonProperty(propertyName: "UpdateBy")]
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        [EpplusIgnore]
        public string Update_by { get; set; }

        //[JsonIgnore]
        [SugarColumn(IsOnlyIgnoreInsert = true)]//设置后插入数据不会有此字段
        [JsonProperty(propertyName: "UpdateTime")]
        [EpplusIgnore]
        public DateTime? Update_time { get; set; }

        public string Remark { get; set; }

        /// <summary>
        /// 搜索时间起始时间
        /// </summary>
        /// <summary>
        /// Write：需穿一个bool值，false时insert，update等操作会忽略此列（和Computed的作用差不多，看了源码也没发现与Computed有什么不一样的地方，有了解的朋友可以赐教下哈）
        /// ExplicitKey：指定此列为主键（不自动增长类型例如guid，ExplicitKey与Key地区别下面会详细讲）
        /// Key：指定此列为主键（自动增长主键），可忽略，忽略后默认查找
        /// [Computed]计算属性，打上此标签，对象地insert，update等操作会忽略此列
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        [EpplusIgnore]
        public DateTime? BeginTime { get; set; }

        /// <summary>
        /// 用于搜索使用
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        [EpplusIgnore]
        public DateTime? EndTime { get; set; }
    }
}
