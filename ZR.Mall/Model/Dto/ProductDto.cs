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
        /// 运费信息（C 端商品详情页的包邮标签）
        /// </summary>
        /// <remarks>
        /// 只在 C 端详情接口回填（见 FrontProductController），后台接口保持 null ——
        /// 后台商品编辑页用的是原始模板 Id，不需要这层展示口径。
        /// </remarks>
        [ExcelColumn(Ignore = true)]
        public FrontFreightInfoDto FreightInfo { get; set; }

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

        /// <summary>
        /// 商品形态：1 实物 / 2 虚拟，默认实物。后台表单绑定与 C 端回填共用本字段。
        /// </summary>
        [ExcelColumn(Name = "商品类型")]
        public ProductTypeEnum ProductType { get; set; } = ProductTypeEnum.Physical;

        /// <summary>
        /// 下单表单字段定义：由商品配置原样下发，C 端据此渲染输入框；为空表示下单无需额外填写。
        /// 虚拟商品的"接收账号/接收手机号"就走这里，不复用收货地址。
        /// 前端只做提示性校验，最终以服务端下单时的校验为准。
        /// </summary>
        [ExcelColumn(Ignore = true)]
        public List<ProductFormItem> FormItems { get; set; }

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