using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 文章目录
    /// </summary>
    [SugarTable("articleCategory")]
    [Tenant("0")]
    public class ArticleCategory
    {
        /// <summary>
        /// 目录id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, ColumnName = "Category_id")]
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }
        [SugarColumn(ColumnName = "create_time")]
        public DateTime CreateTime { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [SugarColumn(IsIgnore = true)]
        public List<ArticleCategory> Children { get; set; }
    }
}
