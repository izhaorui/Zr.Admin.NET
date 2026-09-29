using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 商品规格的中性表示转换：规格组 ↔ 组合快照、规格定义 → 商品摘要。
    ///
    /// 为什么独立成一处：规格组合（"颜色:红;尺寸:XL"）与规格摘要（"颜色: 红/蓝, 尺寸: S/M/L"）
    /// 的格式被商品列表、SKU 表格、前台规格匹配、订单快照多处依赖。
    /// 一旦同一个格式在两处各写一遍，就必然漂移 —— 商品价格区间与 SKU 不同步就是这么产生的。
    /// 这里只做纯格式转换：不访问数据库、不做业务校验、不抛业务异常。
    /// </summary>
    public static class ProductSpecFormatter
    {
        /// <summary>
        /// 规格组集合 → 组合快照，形如 "颜色:红;尺寸:XL"（与 Skus.SpecCombination 的存储格式一致）。
        /// 规格名或值缺失的条目会被跳过；没有有效组合时返回 null。
        /// </summary>
        public static string BuildCombination(IEnumerable<ProductSpecGroup> groups)
        {
            if (groups == null)
            {
                return null;
            }

            var pairs = groups
                .Where(g => g != null && !string.IsNullOrWhiteSpace(g.Name) && !string.IsNullOrWhiteSpace(g.Value))
                .Select(g => $"{g.Name.Trim()}:{g.Value.Trim()}")
                .ToList();

            return pairs.Count > 0 ? string.Join(";", pairs) : null;
        }

        /// <summary>
        /// 组合快照 → 规格组集合；无法识别的片段（缺冒号、名或值为空）直接跳过。
        /// </summary>
        public static List<ProductSpecGroup> ParseCombination(string specCombination)
        {
            var pairs = new List<ProductSpecGroup>();
            if (string.IsNullOrWhiteSpace(specCombination))
            {
                return pairs;
            }

            foreach (var pair in specCombination.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                var name = pair[..separator].Trim();
                var value = pair[(separator + 1)..].Trim();
                if (name.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                pairs.Add(new ProductSpecGroup { Name = name, Value = value });
            }

            return pairs;
        }

        /// <summary>
        /// 规格定义 → 商品规格摘要，形如 "颜色: 红/蓝, 尺寸: S/M/L"。
        /// 定义里存在、但暂时没有任何 SKU 的规格值同样会出现在摘要里（摘要描述的是商品的规格定义，不是已售组合）。
        /// </summary>
        public static string FormatSummary(IEnumerable<(string Name, List<string> Values)> definitions)
        {
            if (definitions == null)
            {
                return string.Empty;
            }

            return string.Join(", ", definitions
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .Select(d => $"{d.Name}: {string.Join("/", d.Values ?? new List<string>())}"));
        }

        /// <summary>
        /// 规格定义（实体 mms_product_spec）→ 商品规格摘要
        /// </summary>
        public static string FormatSummary(IEnumerable<ProductSpec> definitions)
        {
            return definitions == null
                ? string.Empty
                : FormatSummary(definitions.Select(d => (d.Name, d.SpecValues)));
        }

    }
}
