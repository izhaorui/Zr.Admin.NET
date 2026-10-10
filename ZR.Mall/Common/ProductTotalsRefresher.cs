using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 商品级汇总列（总销量 <see cref="Product.TotalSalesVolume"/> / 总库存 <see cref="Product.TotalStock"/>）的回写器。
    ///
    /// 背景：这两列曾是"从未写入"的冗余列（恒为 0），真实数据只存在于 <see cref="Skus"/>。
    /// C 端按销量排序、按库存沉底/过滤若直接读列会静默失效，若每次都用 SKU 相关子查询，
    /// 既慢、又会在 ORDER BY 里踩到 SqlSugar 的外表别名绑定问题。这里统一维护，让查询只读列。
    ///
    /// 维护点（凡是会改变某商品 SKU 销量/库存的地方都要调）：
    /// - 下单扣减（FrontOrderService）、退款/取消回补（OMSOrderStateService.RestoreStock）；
    /// - 后台 SKU 增删改（SkusService.RefreshProductSummary 内顺带回写）。
    ///
    /// 一律"从真相重算"（SUM 未删 SKU）而不是 +/- 计数：重算天然防漂移，
    /// 也不受销量被 IIF 夹到 0 的影响。传入的必须是事务内使用的那个 client，
    /// 否则回写会落到事务之外、与主流程不一致。
    /// </summary>
    public static class ProductTotalsRefresher
    {
        /// <summary>
        /// 重算并回写这些商品的总销量与总库存。空集合直接返回。
        /// </summary>
        public static void Refresh(ISqlSugarClient db, IEnumerable<long> productIds)
        {
            var ids = productIds?.Where(id => id > 0).Distinct().ToList();
            if (db == null || ids == null || ids.Count == 0)
            {
                return;
            }

            var totals = db.Queryable<Skus>()
                .Where(s => ids.Contains(s.ProductId) && s.IsDelete == 0)
                .GroupBy(s => s.ProductId)
                .Select(s => new
                {
                    ProductId = s.ProductId,
                    Sales = SqlFunc.AggregateSum(s.SalesVolume),
                    Stock = SqlFunc.AggregateSum(s.Stock)
                })
                .ToList();
            var totalsMap = totals.ToDictionary(t => t.ProductId);

            foreach (var id in ids)
            {
                // 无可用 SKU 的商品不在分组结果里：两列都应归零，不能保留旧值
                totalsMap.TryGetValue(id, out var total);
                var sales = total?.Sales ?? 0;
                var stock = total?.Stock ?? 0;

                db.Updateable<Product>()
                    .SetColumns(p => new Product { TotalSalesVolume = sales, TotalStock = stock })
                    .Where(p => p.ProductId == id)
                    .ExecuteCommand();
            }
        }
    }
}
