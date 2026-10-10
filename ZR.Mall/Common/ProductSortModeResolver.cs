using ZR.Mall.Enum;

namespace ZR.Mall.Common
{
    /// <summary>
    /// C 端排序档位字符串 → <see cref="ProductSortMode"/> 的解析器。
    ///
    /// 为什么单独成一处：sortMode 由前端以 query 字符串传入，前端版本、调用方习惯（下划线/驼峰/中文别名）
    /// 都可能不一致。这里集中做兼容与容错，缺省或无法识别一律回落"综合"，
    /// 保证契约稳定——前端传错值的最坏结果只是排序不符合预期，而不会 500 或排到空。
    ///
    /// 只做纯字符串解析：不访问数据库、不抛业务异常。
    /// </summary>
    public static class ProductSortModeResolver
    {
        /// <summary>
        /// 解析排序档位。空值、空白或非法值一律回落 <see cref="ProductSortMode.Synthesize"/>（综合）。
        /// 大小写不敏感，兼容多种别名（见方法内映射）。
        /// </summary>
        public static ProductSortMode Resolve(string sortMode)
        {
            if (string.IsNullOrWhiteSpace(sortMode))
            {
                return ProductSortMode.Synthesize;
            }

            // 归一化：去掉首尾空白、下划线/短横线，统一小写，便于全等匹配
            var key = sortMode.Trim().Replace("_", "").Replace("-", "").ToLowerInvariant();

            return key switch
            {
                "synthesize" or "comprehensive" or "default" or "complex" or "综合" => ProductSortMode.Synthesize,
                "sales" or "sale" or "salesvolume" or "hot" or "销量" => ProductSortMode.Sales,
                "priceasc" or "priceup" or "pricelow" or "asc" or "价格升序" or "价格升" => ProductSortMode.PriceAsc,
                "pricedesc" or "pricedown" or "pricehigh" or "desc" or "价格降序" or "价格降" => ProductSortMode.PriceDesc,
                "newest" or "new" or "latest" or "newadd" or "上新" or "最新" => ProductSortMode.Newest,
                // 兼容前端直接传枚举名或数字串（如 "3"），非法值回落到综合
                // 显式限定 System.Enum：当前命名空间树上存在 ZR.Mall.Enum，裸写 Enum 会被解析成该命名空间
                _ => int.TryParse(key, out var numeric) && System.Enum.IsDefined(typeof(ProductSortMode), numeric)
                    ? (ProductSortMode)numeric
                    : ProductSortMode.Synthesize,
            };
        }
    }
}
