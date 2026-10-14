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
    // C 端在售列表：固定 在售 + 未删除，分类页再叠加 CategoryId（见 ProductService.QueryExp）。
    // 多列按同名特性声明，顺序即列序。
    [SugarIndex(Common.MallIndexEnsurer.ProductFront, nameof(SaleStatus), OrderByType.Asc, false)]
    [SugarIndex(Common.MallIndexEnsurer.ProductFront, nameof(IsDelete), OrderByType.Asc, false)]
    [SugarIndex(Common.MallIndexEnsurer.ProductFront, nameof(CategoryId), OrderByType.Asc, false)]
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
        /// 总库存（所有未删 SKU 的 Stock 之和），由 <see cref="Common.ProductTotalsRefresher"/> 维护。
        /// 用途：C 端"缺货沉底 / 仅看有货"直接读本列排序与过滤，避免在 ORDER BY 里用 SKU 相关子查询
        /// （SqlSugar 在 Includes + 分页场景下无法正确绑定子查询里的外表别名，会报 could not be bound）。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int TotalStock { get; set; }

        /// <summary>
        /// 排序ID (越大排序越靠前)
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int SortId { get; set; } = 0;

        /// <summary>
        /// 置顶生效开始时间（为空表示不限开始时间）。与 <see cref="TopEndTime"/> 共同实现"置顶时效"：
        /// 只有当前时间落在 [开始, 结束] 窗口内，<see cref="SortId"/> 才参与 C 端综合排序。
        /// </summary>
        public DateTime? TopBeginTime { get; set; }

        /// <summary>
        /// 置顶生效结束时间（为空表示长期有效）。超出窗口后置顶自动失效，无需人工下架。
        /// </summary>
        public DateTime? TopEndTime { get; set; }

        /// <summary>
        /// 首页场景权重（越大越靠前）：C 端"首页/推荐位"（无分类、无关键词）综合排序时使用。
        /// </summary>
        /// <remarks>
        /// 分场景权重用独立数值列，而不是 JSON 列：JSON 子属性无法可靠地用于 SQL 的 ORDER BY
        /// （需按数据库方言写 JSON_EXTRACT/JSON_VALUE，跨库不稳），列则天然可排序、可加索引。
        /// </remarks>
        [SugarColumn(DefaultValue = "0")]
        public int HomeWeight { get; set; } = 0;

        /// <summary>
        /// 分类场景权重（越大越靠前）：C 端按分类筛选且无关键词时使用。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int CategoryWeight { get; set; } = 0;

        /// <summary>
        /// 搜索场景权重（越大越靠前）：C 端带关键词搜索时使用（相关性分级仍优先于它）。
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int SearchWeight { get; set; } = 0;

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
        /// 下单表单字段定义：买家下单时需要额外填写的输入项（存 JSON 列）。
        /// 为空表示"下单无需额外填写"；虚拟商品用来收"接收账号/接收手机号"等，实物也能用（如预约配送时间）。
        /// 规则与字段类型见 <see cref="Common.ProductFormValidator"/>、<see cref="Enum.ProductFormType"/>。
        /// </summary>
        /// <remarks>
        /// 与形态（<see cref="ProductTypeEnum"/>）无关：形态决定地址与运费，表单决定"让买家填什么"，两者正交。
        /// 后台保存时由 ProductService 调 ValidateDefinition 校验，脏配置不会落库。
        /// 列类型显式指定为长文本：字段多了 JSON 会变长（如 20 个字段各带 label/options/pattern），
        /// 交给默认值容易撞上短列上限。
        /// </remarks>
        [SugarColumn(IsJson = true, ColumnDataType = StaticConfig.CodeFirst_BigString, ColumnDescription = "下单表单字段")]
        public List<ProductFormItem> FormItems { get; set; }

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

    /// <summary>
    /// 商品下单表单的字段定义（存 JSON 列，见 <see cref="Product.FormItems"/>）。
    /// 一条定义 = 一个输入框；下单时按 <see cref="Field"/> 回传值，值随订单项快照落库。
    /// </summary>
    public class ProductFormItem
    {
        /// <summary>
        /// 字段键：下单回传与快照的匹配依据，同一商品内唯一；字母开头，只含字母/数字/下划线。
        /// 之所以不用中文：键要拼进 JSON 下发给前端当表单 key。
        /// </summary>
        public string Field { get; set; }

        /// <summary>显示名（给买家看）；为空时回落为 <see cref="Field"/></summary>
        public string Label { get; set; }

        /// <summary>
        /// 字段类型，取值见 <see cref="Enum.ProductFormType"/>。
        /// 未收录的类型按文本框处理（不报错），后台加新类型不必先改服务端。
        /// </summary>
        public string Type { get; set; }

        /// <summary>是否必填</summary>
        public bool Required { get; set; }

        /// <summary>候选项（select/radio/checkbox 必填），逗号分隔、label 即 value</summary>
        public string Options { get; set; }

        /// <summary>
        /// 自定义校验正则（可选）。给了就优先于类型自带规则。
        /// 保存时校验正则能否编译 —— 写错一次就是所有买家都提交失败。
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// 长度上限（可选）。缺省按类型给默认值（单行 200 / 多行 1000），
        /// 且运行期不超过 <see cref="Common.ProductFormValidator.ValueHardLimit"/>。
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>输入框占位提示</summary>
        public string Placeholder { get; set; }

        /// <summary>
        /// 该字段是否收集敏感信息（手机号 / 身份证 / 银行卡 / 邮箱等）。
        /// 决定后台订单详情里它是**明文展示**还是**默认脱敏 + 需点「显示完整内容」**。
        /// </summary>
        /// <remarks>
        /// 用 <c>bool?</c> 而不是 <c>bool</c>，是为了区分"没设置"与"运营主动取消"：
        /// - <c>null</c>：未设置，保存时按类型推断（见 <see cref="Enum.ProductFormType.DefaultSensitive"/>）；
        /// - <c>true</c>：后台订单详情默认脱敏，需点「显示完整内容」才明文；
        /// - <c>false</c>：运营明确取消 —— 典型是把"接收账号"这类必须看清的字段设为不敏感。
        ///
        /// 无论这里是什么值，服务端都会对**值本身**兜底：命中身份证 / 银行卡格式的一律打码，
        /// 防止运营忘勾就把敏感信息明文摆出来（见 <see cref="Common.OrderFormValueHelper"/>）。
        /// </remarks>
        public bool? Sensitive { get; set; }

        /// <summary>排序（小在前）</summary>
        public int Sort { get; set; }
    }
}