
namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 商品库存查询对象
    /// </summary>
    public class ShoppingSkusQueryDto : PagerInfo
    {
        /// <summary>
        /// 商品ID（按商品筛选）
        /// </summary>
        public long? ProductId { get; set; }

        /// <summary>
        /// 商品编码（按商品编码检索 SKU，业务上通常按编码定位具体商品）
        /// </summary>
        public string ProductCode { get; set; }
    }

    /// <summary>
    /// 商品库存输入输出对象
    /// </summary>
    public class SkusDto
    {
        public long? SkuId { get; set; }

        [Required(ErrorMessage = "商品ID不能为空")]
        public long ProductId { get; set; }

        /// <summary>
        /// 商品名称（列表 join 返回，用于表格展示，无需前端预载全量商品）
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 商品编码（列表 join 返回，用于按编码检索/展示）
        /// </summary>
        public string ProductCode { get; set; }


        [Required(ErrorMessage = "售卖价格不能为空")]
        public decimal Price { get; set; }
        public int SalesVolume { get; set; }
        public int Stock { get; set; }
        public int SortId { get; set; }
        /// <summary>
        /// 重量（千克 (Kg)）
        /// </summary>
        public decimal Weight { get; set; }
        public string ImageUrl { get; set; }
        ///// <summary>
        ///// 暂时没用到
        ///// </summary>
        //public string SpecValue { get; set; }
        public string SpecCombination { get; set; }
        /// <summary>
        /// 规格
        /// </summary>
        [SugarColumn(IsJson = true)]
        public List<ProductSpecGroup> Specs { get; set; }
    }
}