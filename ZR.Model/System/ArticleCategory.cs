using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 文章目录
    /// </summary>
    [SqlSugar.SugarTable("articleCategory")]
    public class ArticleCategory
    {
        public int Category_Id { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public List<ArticleCategory> Children { get; set; }
    }
}
