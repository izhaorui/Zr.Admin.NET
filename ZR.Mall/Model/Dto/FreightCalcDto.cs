namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// C 端运费试算入参
    /// </summary>
    /// <remarks>
    /// 刻意**不含运费字段**：运费只能由服务端算，前端传什么都不采信。
    /// </remarks>
    public class CalcFreightDto
    {
        /// <summary>参与试算的商品行</summary>
        public List<FreightCheckoutItem> Items { get; set; } = new();

        /// <summary>
        /// 商品总额（整单口径，不含运费）
        /// </summary>
        /// <remarks>
        /// 由前端传入而非服务端重算：试算时订单尚未创建，服务端无从得知各行单价
        /// （价格在下单时才锁定）。允许前端传，因为**它只影响包邮门槛判断**，
        /// 最终运费下单时仍按服务端重算的商品总额为准，两者不一致只会导致
        /// "试算显示包邮、下单收 8 元"，不会造成少收。
        /// </remarks>
        public decimal? GoodsAmount { get; set; }

        /// <summary>收货地区（名称 + 编码；编码缺失时服务端按名称兜底反查）</summary>
        public AddressSnapshot Address { get; set; }
    }

    /// <summary>
    /// 运费试算/下单的商品行入参（对外契约）
    /// </summary>
    /// <remarks>
    /// C 端只传 skuId 与数量，**不传运费** —— 运费一律由服务端算，
    /// 杜绝前端传什么就是什么。
    /// </remarks>
    public class FreightCheckoutItem
    {
        /// <summary>SKU Id</summary>
        public long SkuId { get; set; }

        /// <summary>数量（件）</summary>
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 运费计算入参（引擎契约）
    /// </summary>
    /// <remarks>
    /// 由 FreightService 组装：它负责批量查 sku / product / 模板 / 排除地区，
    /// 然后把结果摊平成这个结构交给纯函数引擎 FreightCalculator。
    /// 引擎不碰数据库，因此所有边界都能被单元测试直接覆盖。
    /// </remarks>
    public class FreightCalcRequest
    {
        /// <summary>参与计费的商品行</summary>
        public List<FreightCalcItem> Items { get; set; } = new();

        /// <summary>商品总额（整单口径，= Σ 单价×数量，不含运费）</summary>
        /// <remarks>用于包邮门槛判断。必须是整单金额而非单模板金额：
        /// 门槛语义是"这一单买够了就包邮"，按单模板算会因拆单而永远达不到门槛。</remarks>
        public decimal GoodsAmount { get; set; }

        /// <summary>收货省级行政编码；为空表示地区无法判定（不参与地区匹配）</summary>
        public string ProvinceCode { get; set; }

        /// <summary>收货市级行政编码</summary>
        public string CityCode { get; set; }

        /// <summary>收货区县级行政编码</summary>
        public string DistrictCode { get; set; }
    }

    /// <summary>
    /// 参与计费的单行商品
    /// </summary>
    public class FreightCalcItem
    {
        /// <summary>SKU Id（仅用于日志与明细定位，不参与计算）</summary>
        public long SkuId { get; set; }

        /// <summary>数量（件）</summary>
        public int Quantity { get; set; }

        /// <summary>单件重量（kg），取自 Skus.Weight</summary>
        /// <remarks>0 视为不贡献重量（不额外收运费）；负数按 0 处理</remarks>
        public decimal Weight { get; set; }

        /// <summary>命中的运费模板</summary>
        /// <remarks>为 null 表示商品未绑定模板且无默认模板 → 该行不计运费，
        /// 但仍计入 NoTemplate 统计以便留痕。</remarks>
        public FreightTemplate Template { get; set; }

        /// <summary>
        /// 该模板配置的全部不配送地区
        /// </summary>
        /// <remarks>
        /// 由引擎自己按 <see cref="FreightCalcRequest.ProvinceCode"/> 等收货编码判定命中，
        /// 调用方**不要**预先过滤 —— 预过滤一旦漏一处，就会把"未配置的地区"误判成不可配送，
        /// 直接拦掉本可成交的订单。
        /// </remarks>
        public List<FreightTemplateExcludeRegion> ExcludeRegions { get; set; } = new();
    }

    /// <summary>
    /// 运费计算结果
    /// </summary>
    public class FreightCalcResult
    {
        /// <summary>合计运费（已按多模板分段求和并应用包邮减免）</summary>
        public decimal Freight { get; set; }

        /// <summary>商品总额（回显用，避免调用方自己再算一遍导致口径不一致）</summary>
        public decimal GoodsAmount { get; set; }

        /// <summary>因包邮门槛达标而免除运费（用于 C 端提示"已包邮"）</summary>
        public bool FreeShipping { get; set; }

        /// <summary>按模板分段的命中明细，供 C 端展示与后台对账</summary>
        public List<FreightTemplateHit> Details { get; set; } = new();

        /// <summary>命中的不配送地区名称（非空即表示不可下单，由服务层转成异常）</summary>
        public List<string> UndeliverableRegionNames { get; set; } = new();

        /// <summary>未绑定模板（含无默认模板）而计 0 运费的行数，用于日志留痕</summary>
        public int NoTemplate { get; set; }

        /// <summary>
        /// 行 → 命中模板 的映射（SkuId → 模板）
        /// </summary>
        /// <remarks>
        /// 由 <c>FreightService</c> 填充（引擎只做模板级聚合，摊平时已丢失 SKU 维度）。
        /// 下单时据此给每个订单项盖上模板快照，事后可反查运费构成；
        /// 不在映射里的行表示未绑定模板且无默认模板 —— 如实留痕为 null。
        /// </remarks>
        public Dictionary<long, FreightTemplate> SkuTemplates { get; set; } = new();
    }

    /// <summary>
    /// 单个模板的计费命中明细
    /// </summary>
    public class FreightTemplateHit
    {
        /// <summary>模板 Id；NoTemplate 为 true 时为 0</summary>
        public long TemplateId { get; set; }

        /// <summary>模板名称；未绑定时为 null</summary>
        public string TemplateName { get; set; }

        /// <summary>该模板命中的总件数</summary>
        public int TotalPiece { get; set; }

        /// <summary>该模板命中的总重量（kg）</summary>
        public decimal TotalWeight { get; set; }

        /// <summary>该模板的运费（已含包邮减免）</summary>
        public decimal Freight { get; set; }

        /// <summary>该模板是否因包邮门槛达标而免运费</summary>
        public bool FreeShipping { get; set; }

        /// <summary>是否属于"未绑定模板、计 0 运费"的兜底分段</summary>
        public bool NoTemplate { get; set; }
    }
}
