namespace ZR.Mall.Model
{
    /// <summary>
    /// 商城目录
    /// </summary>
    [SugarTable("mms_category", "商城目录")]
    [Tenant("MallDb")]
    public class Category
    {
        /// <summary>
        /// 目录id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "category_id")]
        public int CategoryId { get; set; }

        [SugarColumn(ColumnDescription = "目录名", Length = 20, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Name { get; set; }
        [SugarColumn(ColumnDescription = "图标")]
        public string Icon { get; set; }
        /// <summary>
        /// 排序id
        /// </summary>
        public int OrderNum { get; set; }
        public int? ParentId { get; set; }

        /// <summary>
        /// 说明
        /// </summary>
        public string Introduce { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDelete { get; set; }
        /// <summary>
        /// 是否显示
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public int ShowStatus { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(ColumnDescription = "创建时间", ColumnName = "create_time", InsertServerTime = true)]
        public DateTime? CreateTime { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [SugarColumn(IsIgnore = true)]
        public List<Category> Children { get; set; }
    }
}
