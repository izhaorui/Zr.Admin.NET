using ZR.Mall.Enum;
using ZR.Model.System;

namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 商品管理查询对象
    /// </summary>
    public class ShoppingProductQueryDto : PagerInfo
    {
        public long? ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductCode { get; set; }
        public SaleStatus? SaleStatus { get; set; }
        public int? CategoryId { get; set; }
        public int? BrandId { get; set; }
        public DateTime? BeginAddTime { get; set; }
        public DateTime? EndAddTime { get; set; }
    }

    /// <summary>
    /// 商品管理输入输出对象
    /// </summary>
    public class ProductDto : SysBase
    {
        [ExcelColumn(Name = "商品ID")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "商品名不能为空")]
        [ExcelColumn(Name = "商品名")]
        public string ProductName { get; set; }

        [ExcelColumn(Name = "介绍")]
        public string Introduce { get; set; }

        [ExcelColumn(Ignore = true)]
        public int? CategoryId { get; set; }

        [ExcelColumn(Name = "商品分类")]
        public string CategoryName { get; set; }

        [ExcelColumn(Ignore = true)]
        public long? BrandId { get; set; }

        [ExcelColumn(Name = "品牌")]
        public string BrandName { get; set; }

        /// <summary>
        /// 运费模板 Id；为空表示走默认模板（无默认模板则计 0 运费）
        /// </summary>
        [ExcelColumn(Ignore = true)]
        public long? FreightTemplateId { get; set; }

        /// <summary>
        /// 运费模板名称（列表与详情回显用，避免前端再查一次模板表）
        /// </summary>
        [ExcelColumn(Ignore = true)]
        public string FreightTemplateName { get; set; }

        [ExcelColumn(Name = "sku最低价格")]
        public decimal Price { get; set; }

        [ExcelColumn(Name = "sku最高价格")]
        public decimal MaxPrice { get; set; }

        [ExcelColumn(Name = "封面地址")]
        public string ImageUrls { get; set; }

        [ExcelColumn(Name = "商品主题")]
        public string MainImage { get; set; }

        [ExcelColumn(Name = "视频介绍")]
        public string VideoUrl { get; set; }

        [ExcelColumn(Name = "排序ID")]
        public int SortId { get; set; }

        [ExcelColumn(Name = "售卖状态", Ignore = true)]
        public SaleStatus SaleStatus { get; set; }

        [ExcelColumn(Name = "详情", Ignore = true)]
        public string DetailsHtml { get; set; }

        [ExcelColumn(Name = "总库存")]
        public int Stock { get; set; }

        [ExcelColumn(Name = "总销量")]
        public int TotalSalesVolume { get; set; }

        [ExcelColumn(Name = "规格")]
        public string SpecSummary { get; set; }

        [ExcelColumn(Name = "添加时间", Format = "yyyy-MM-dd HH:mm:ss", Width = 20)]
        public DateTime? AddTime { get; set; }

        [ExcelColumn(Name = "是否删除")]
        public int IsDelete { get; set; }

        [ExcelColumn(Name = "限购")]
        public PurchaseLimit PurchaseLimit { get; set; }

        [ExcelColumn(Name = "规格类型")]
        public SpecType SpecType { get; set; } = SpecType.Multiple;

        [ExcelColumn(Ignore = true)]
        public List<SkusDto> Skus { get; set; }

        [ExcelColumn(Ignore = true)]
        public Category Category { get; set; }

        [ExcelColumn(Ignore = true)]
        public List<ProductSpecDto> Spec { get; set; }
        [ExcelColumn(Ignore = true)]
        public Brand Brand { get; set; }
    }
}