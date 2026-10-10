namespace ZR.Mall.Enum
{
    /// <summary>
    /// C 端商品列表排序档位（前端可切换，见 FrontProductController.list 的 sortMode 参数）。
    ///
    /// 约定：
    /// - <see cref="Synthesize"/>（综合）是默认档，也是唯一会使用运营置顶权重 <c>Product.SortId</c> 的档位；
    ///   其余档位只按各自字段排序，避免"选了按价格排却被置顶商品插队"的困惑。
    /// - 所有档位统一以 <c>ProductId DESC</c> 兜底，保证分页稳定（同值时不会在页间跳动或重复）。
    /// - 权重 <c>SortId</c> 语义为"越大越靠前"，与后台商品列表 ChangeSort 接口一致。
    /// </summary>
    public enum ProductSortMode
    {
        /// <summary>综合（默认）：SortId DESC → 销量 DESC → 上新时间 DESC → ProductId DESC</summary>
        Synthesize = 0,

        /// <summary>销量：销量 DESC → ProductId DESC</summary>
        Sales = 1,

        /// <summary>价格升序：Price ASC → ProductId DESC</summary>
        PriceAsc = 2,

        /// <summary>价格降序：Price DESC → ProductId DESC</summary>
        PriceDesc = 3,

        /// <summary>上新：上架时间 DESC → ProductId DESC</summary>
        Newest = 4,
    }
}
