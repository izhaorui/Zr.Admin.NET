namespace ZR.Mall.Model
{
    /// <summary>
    /// 商品品牌
    /// </summary>
    [SugarTable("mms_brand", "品牌表")]
    [Tenant("MallDb")]
    public class Brand
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }
        /// <summary>
        /// 品牌名
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 品牌logo
        /// </summary>
        public string Logo { get; set; }
        /// <summary>
        /// 品牌介绍
        /// </summary>
        public string Description { get; set; }
    }
}
