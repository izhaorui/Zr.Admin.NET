namespace ZR.Mall.Enum
{
    /// <summary>
    /// C 端商品展示场景：决定"综合"排序档使用哪一列分场景权重。
    ///
    /// 场景由请求参数推断（同一列表接口服务首页/分类/搜索）：
    /// 带关键词=搜索；带分类=分类；其余=首页/推荐位。见 ProductService.ResolveScene。
    /// </summary>
    public enum ProductScene
    {
        /// <summary>首页 / 推荐位（无分类、无关键词）→ Product.HomeWeight</summary>
        Home = 1,

        /// <summary>分类筛选（带分类、无关键词）→ Product.CategoryWeight</summary>
        Category = 2,

        /// <summary>关键词搜索 → Product.SearchWeight</summary>
        Search = 3,
    }
}
