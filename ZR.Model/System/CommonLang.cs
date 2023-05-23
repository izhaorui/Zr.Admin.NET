using Newtonsoft.Json;
using SqlSugar;
using System;
using System.ComponentModel;

namespace ZR.Model.Models
{
    /// <summary>
    /// 多语言配置，数据实体对象
    /// </summary>
    [Tenant("0")]
    [SugarTable("sys_common_lang")]
    public class CommonLang
    {
        /// <summary>
        /// id
        /// </summary>
        [JsonConverter(typeof(ValueToStringConverter))]
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 语言code
        /// </summary>
        [DisplayName("语言code")]
        [SugarColumn(ColumnName = "lang_code", IsNullable = false)]
        public string LangCode { get; set; }

        /// <summary>
        /// 语言key
        /// </summary>
        [DisplayName("语言key")]
        [SugarColumn(ColumnName = "lang_key")]
        public string LangKey { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [DisplayName("名称")]
        [SugarColumn(ColumnName = "lang_name", IsNullable = false)]
        public string LangName { get; set; }

        /// <summary>
        /// 添加时间
        /// </summary>
        [DisplayName("添加时间")]
        public DateTime? Addtime { get; set; }
    }
}