using ZR.Mall.Model.Dto;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 商品库存
    /// </summary>
    [SugarTable("mms_skus", "商品库存sku")]
    [Tenant("MallDb")]
    public class Skus
    {
        /// <summary>
        /// SkuId 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long SkuId { get; set; }

        /// <summary>
        /// 商品ID 
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long ProductId { get; set; }

        /// <summary>
        /// 售卖价格 
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public decimal Price { get; set; }

        /// <summary>
        /// 库存 
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Stock { get; set; }

        /// <summary>
        /// 销量
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int SalesVolume { get; set; }

        /// <summary>
        /// 缩略图片 
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// 规格值id
        /// </summary>
        public string SpecIds { get; set; }
        /// <summary>
        /// 快照存储,规格 如 "颜色:红;尺寸:XL"
        /// </summary>
        public string SpecCombination { get; set; }
        /// <summary>
        /// 添加时间
        /// </summary>
        [SugarColumn(InsertServerTime = true, IsOnlyIgnoreUpdate = true)]
        public DateTime AddTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [SugarColumn(UpdateServerTime = true, IsOnlyIgnoreInsert = true)]
        public DateTime UpdateTime { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDelete { get; set; }

        /// <summary>
        /// 排序id
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int SortId { get; set; }

        /// <summary>
        /// 重量（千克 (Kg)）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public decimal Weight { get; set; }

        /// <summary>
        /// 版本号（防止并发更新）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Version { get; set; }

        /// <summary>
        /// 规格
        /// </summary>
        [SugarColumn(IsJson = true)]
        public List<ProductSpecGroup> Specs { get; set; }
    }
}