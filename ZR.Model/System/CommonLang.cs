using Newtonsoft.Json;
using SqlSugar;
using System;
using System.ComponentModel;

namespace ZR.Model.Models
{
    /// <summary>
    /// 多语言配置，数据实体对象
    ///
    /// @author zr
    /// @date 2022-05-06
    /// </summary>
    [Tenant("0")]
    [SugarTable("sys_common_lang")]
    public class CommonLang
    {
        /// <summary>
        /// 描述 : id
        /// 空值 : false  
        /// </summary>
        [JsonConverter(typeof(ValueToStringConverter))]
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 描述 : 语言code
        /// 空值 : false  
        /// </summary>
        [DisplayName("语言code")]
        [SugarColumn(ColumnName = "lang_code")]
        public string LangCode { get; set; }

        /// <summary>
        /// 描述 : 语言key
        /// 空值 : true  
        /// </summary>
        [DisplayName("语言key")]
        [SugarColumn(ColumnName = "lang_key")]
        public string LangKey { get; set; }

        /// <summary>
        /// 描述 : 名称
        /// 空值 : false  
        /// </summary>
        [DisplayName("名称")]
        [SugarColumn(ColumnName = "lang_name")]
        public string LangName { get; set; }

        /// <summary>
        /// 描述 : 添加时间
        /// 空值 : true  
        /// </summary>
        [DisplayName("添加时间")]
        public DateTime? Addtime { get; set; }
    }
}