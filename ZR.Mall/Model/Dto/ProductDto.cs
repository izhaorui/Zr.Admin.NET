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

        /// <summary>
        /// C 端排序档位（仅前台列表 FrontProductController.list 生效，后台列表忽略）。
        /// 取值 token：comprehensive/synthesize(综合，默认)、sales(销量)、price_asc(价格升)、
        /// price_desc(价格降)、newest(上新)；空值或非法值一律回落"综合"。
        /// 解析见 <see cref="Common.ProductSortModeResolver"/>，各档排序键见 <see cref="Enum.ProductSortMode"/>。
        /// </summary>
        public string SortMode { get; set; }

        /// <summary>
        /// C 端搜索关键词（仅前台列表生效）：命中 商品名 / 商品编码 / 简介 任一即返回，
        /// 且命中商品名的排在仅命中编码/简介的之前（相关性分级，见 ProductService.ApplyFrontSorting）。
        /// 与 <see cref="ProductName"/> 的区别：ProductName 是后台按商品名的精确筛选，本字段是前台模糊搜索的入口。
        /// </summary>
        public string Keyword { get; set; }

        /// <summary>
        /// 仅看有货（仅前台列表生效）：为 true 时过滤掉所有 SKU 均无可售库存的商品。
        /// 默认 false —— 缺货商品不隐藏，只沉到列表末尾（见 ProductService.ApplyFrontSorting 的缺货沉底）。
        /// </summary>
        public bool OnlyInStock { get; set; }
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

        /// <summary>置顶生效开始时间（为空表示不限）</summary>
        [ExcelColumn(Name = "置顶开始", Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? TopBeginTime { get; set; }

        /// <summary>置顶生效结束时间（为空表示长期有效）</summary>
        [ExcelColumn(Name = "置顶结束", Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? TopEndTime { get; set; }

        /// <summary>首页场景权重（越大越靠前）</summary>
        [ExcelColumn(Name = "首页权重")]
        public int HomeWeight { get; set; }

        /// <summary>分类场景权重（越大越靠前）</summary>
        [ExcelColumn(Name = "分类权重")]
        public int CategoryWeight { get; set; }

        /// <summary>搜索场景权重（越大越靠前）</summary>
        [ExcelColumn(Name = "搜索权重")]
        public int SearchWeight { get; set; }

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