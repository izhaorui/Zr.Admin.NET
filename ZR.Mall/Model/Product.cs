using ZR.Mall.Enum;
using ZR.Model.System;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 商品管理
    /// </summary>
    [SugarTable("mms_product", "商品管理")]
    [Tenant("MallDb")]
    public class Product : SysBase
    {
        /// <summary>
        /// 商品ID 
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long ProductId { get; set; }

        /// <summary>
        /// 商品名 
        /// </summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string ProductName { get; set; }

        /// <summary>
        /// 商品编码
        /// </summary>
        public string ProductCode { get; set; }

        /// <summary>
        /// 介绍 
        /// </summary>
        public string Introduce { get; set; }

        /// <summary>
        /// 品牌Id
        /// </summary>
        public long? BrandId { get; set; }

        /// <summary>
        /// 商品分类 
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// sku最低价格(自动根据sku计算)
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 最高价格(用于前端搜索价格区间)自动根据sku计算
        /// </summary>
        public decimal MaxPrice { get; set; }
        /// <summary>
        /// 商品原件（划线价）
        /// </summary>
        public decimal OriginalPrice { get; set; }

        /// <summary>
        /// 单位 （如：件，瓶，斤）
        /// </summary>
        public string Unit { get; set; }
        /// <summary>
        /// 轮播图片 
        /// </summary>
        [SugarColumn(Length = 2000)]
        public string ImageUrls { get; set; }

        /// <summary>
        /// 商品主图
        /// </summary>
        public string MainImage { get; set; }

        /// <summary>
        /// 视频介绍
        /// </summary>
        public string VideoUrl { get; set; }

        /// <summary>
        /// 销量统计（可加缓存）
        /// </summary>
        public int TotalSalesVolume { get; set; }

        /// <summary>
        /// 排序ID (越大排序越靠前)
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int SortId { get; set; } = 0;

        /// <summary>
        /// 显示状态 1.在售 0.下架
        /// </summary>
        [SugarColumn(DefaultValue = "0", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public SaleStatus SaleStatus { get; set; }

        /// <summary>
        /// 添加时间 
        /// </summary>
        [SugarColumn(InsertServerTime = true)]
        public DateTime? AddTime { get; set; }

        /// <summary>
        /// 详情页富文本
        /// </summary>
        [SugarColumn(ColumnDataType = StaticConfig.CodeFirst_BigString)]
        public string DetailsHtml { get; set; }

        /// <summary>
        /// 是否删除
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int IsDelete { get; set; }

        /// <summary>
        /// 规格简介(只读，存档使用)
        /// </summary>
        public string SpecSummary { get; set; }

        /// <summary>
        /// 规格类型
        /// </summary>
        public SpecType SpecType { get; set; } = SpecType.Multiple;

        /// <summary>
        /// 限购
        /// </summary>
        [SugarColumn(IsJson = true)]
        public PurchaseLimit PurchaseLimit { get; set; } = new PurchaseLimit();

        /// <summary>
        /// sku
        /// </summary>
        [Navigate(NavigateType.OneToMany, nameof(ProductId), nameof(Model.Skus.ProductId))] //自定义关系映射
        public List<Skus> Skus { get; set; }

        /// <summary>
        /// 分类
        /// </summary>
        [Navigate(NavigateType.OneToOne, nameof(CategoryId), nameof(Model.Category.CategoryId))] //自定义关系映射
        public Category Category { get; set; }

        /// <summary>
        /// 品牌
        /// </summary>
        [Navigate(NavigateType.OneToOne, nameof(BrandId), nameof(Model.Brand.Id))] //自定义关系映射
        public Brand Brand { get; set; }
    }

    public class PurchaseLimit
    {
        public bool Limit { get; set; }
        /// <summary>
        /// 总限购件数
        /// </summary>
        public int TotalLimit { get; set; }
        /// <summary>
        /// 每单限购
        /// </summary>
        public int SingleLimit { get; set; }

    }
}