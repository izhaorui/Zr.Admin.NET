using ZR.Mall.Enum;
using ZR.Model.System;

namespace ZR.Mall.Model
{
    /// <summary>
    /// 商品管理
    /// </summary>
    [SugarTable("mms_product", "商品管理")]
    // 删除运费模板前的引用校验、列表页「已绑商品数」都按它查
    [SugarIndex("idx_mms_product_freight", nameof(FreightTemplateId), OrderByType.Asc, false)]
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
        /// 商品形态：1 实物 / 2 虚拟（纯服务类），默认实物。见 <see cref="ProductTypeEnum"/>。
        /// 注：虚拟商品免运费由 FreightService 按形态跳过，留空运费模板不是免运费（留空 = 回落默认模板）。
        /// </summary>
        [SugarColumn(DefaultValue = "1")]
        public ProductTypeEnum ProductType { get; set; } = ProductTypeEnum.Physical;

        /// <summary>
        /// 运费模板 Id
        /// </summary>
        /// <remarks>
        /// 为 null 时下单按**默认模板**计费；默认模板也不存在则计 0 运费。
        /// 留空是合法配置（赠品等本就不需要运费），故可空。
        /// </remarks>
        public long? FreightTemplateId { get; set; }

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

        /// <summary>
        /// 运费模板
        /// </summary>
        [Navigate(NavigateType.OneToOne, nameof(FreightTemplateId), nameof(Model.FreightTemplate.Id))] //自定义关系映射
        public FreightTemplate FreightTemplate { get; set; }
    }

    /// <summary>
    /// 商品限购配置（存 JSON 列，见 <see cref="Product.PurchaseLimit"/>）。
    /// </summary>
    public class PurchaseLimit
    {
        /// <summary>
        /// 是否开启限购。为 false 时下面两项均应视为 0（不限制）。
        /// </summary>
        public bool Limit { get; set; }

        /// <summary>
        /// 总限购件数（同一身份累计购买上限）。
        /// 已实现：后端 FrontOrderService.CreateOrder 校验，见其 QueryBoughtQuantity 的口径说明。
        /// <para>口径：<b>不累计</b>已取消(Cancel) / 已关闭(Closed) / 已退款完成(Refunded / Returned)的订单 ——
        /// 取消或退款成功即释放额度；退款中(Refunding) / 退货中(Returning)仍计入。</para>
        /// <para>身份：只按会员 MemberId 累计（与"我的订单"归属口径一致）。</para>
        /// <para>前端暂未按它限制数量（商品详情页的「最多可购 N 件」只反映 SingleLimit 与库存），
        /// 因此超总限购要提交后才由服务端拒绝。</para>
        /// </summary>
        public int TotalLimit { get; set; }

        /// <summary>
        /// 每单限购件数（单次下单内该商品的合计件数上限）。
        /// 已实现：后端 OMSOrderService.CreateOrder 校验，前端商品详情页 maxQuantity 限制。
        /// 按商品维度判断 —— 同一商品拆成多个 SKU 行会先合并件数再比对。
        /// 两者都生效，以服务端为准。
        /// </summary>
        public int SingleLimit { get; set; }
    }
}