namespace ZR.Mall.Model
{
    /// <summary>
    /// 商品规格
    /// </summary>
    [SugarTable("mms_product_spec", "商品规格")]
    [Tenant("MallDb")]
    public class ProductSpec
    {
        /// <summary>
        /// Id 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 商品ID 
        /// </summary>
        public long ProductId { get; set; }

        /// <summary>
        /// 规格名称 
        /// </summary>
        [SugarColumn(ColumnName = "SpecName")]
        public string Name { get; set; }

        /// <summary>
        /// 规格值
        /// </summary>
        [SugarColumn(IsJson = true)]
        public List<string> SpecValues { get; set; }
    }
}