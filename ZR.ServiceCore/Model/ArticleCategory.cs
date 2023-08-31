using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ZR.Model.System
{
    /// <summary>
    /// 文章目录
    /// </summary>
    [SugarTable("articleCategory", "文章目录")]
    [Tenant("0")]
    public class ArticleCategory
    {
        /// <summary>
        /// 目录id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "category_id")]
        public int CategoryId { get; set; }

        [SugarColumn(ColumnDescription = "目录名", Length = 20, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Name { get; set; }
        public int? ParentId { get; set; }
        [SugarColumn(ColumnDescription = "创建时间", ColumnName = "create_time")]
        public DateTime? CreateTime { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [SugarColumn(IsIgnore = true)]
        public List<ArticleCategory> Children { get; set; }
    }
}
