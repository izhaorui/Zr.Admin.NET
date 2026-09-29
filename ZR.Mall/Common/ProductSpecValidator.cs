using Infrastructure;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 商品规格数据校验（写入前拦截，避免再建出"同名规格组"这类歧义数据）。
    ///
    /// 为什么同名规格是脏数据：规格名是整条链路的业务标识 ——
    /// SKU 的规格组合按 "名:值" 拼接（Skus.SpecCombination）、订单项快照按名记录（skuSpec）、
    /// 前台商品详情按名匹配选中规格、后台 SKU 表格按名取列（el-table 按 spec.name 查找），
    /// 一旦同一商品出现两个同名规格组，这些"按名匹配"的地方都会串数据：
    /// 前台永远选不满规格、无法确定 SKU（曾出现 productId=3 存在两个"尺寸"）。
    ///
    /// 比较规则：Trim 后忽略大小写比较（"尺寸 " 与 "尺寸"、"Size" 与 "size" 均视为重复），
    /// 因为下游匹配同样是按字符串相等判断，换个写法存两份只会制造同样的歧义。
    /// </summary>
    public static class ProductSpecValidator
    {
        /// <summary>
        /// 校验规格组集合（商品新增/编辑入口使用）。
        /// 注意：必须在本方法外层的事务（UseTran）之前调用 ——
        /// UseTran 会把异常吞成 result.ErrorMessage，之后统一抛"添加/修改失败"，
        /// 用户将看不到具体哪条规格有问题。
        /// </summary>
        /// <param name="specs">规格组列表，允许为 null/空（无规格商品）</param>
        /// <exception cref="CustomException">规格名重复，或同一规格组内规格值重复</exception>
        public static void Validate(List<ProductSpecDto> specs)
        {
            if (specs == null || specs.Count == 0)
            {
                return;
            }

            Validate(specs.Select(it => (it.Name, it.SpecValues)));
        }

        /// <summary>
        /// 校验规格组集合（直接操作实体的底层写入路径使用）。
        /// </summary>
        public static void Validate(List<ProductSpec> specs)
        {
            if (specs == null || specs.Count == 0)
            {
                return;
            }

            Validate(specs.Select(it => (it.Name, it.SpecValues)));
        }

        private static void Validate(IEnumerable<(string Name, List<string> Values)> specs)
        {
            // key 为 Trim 后的规格名，value 保留首次出现的原始写法用于报错定位
            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var duplicated = new List<string>();

            foreach (var (name, values) in specs)
            {
                var key = (name ?? string.Empty).Trim();
                // 空规格名由前端表单/其它校验负责，这里只处理"重复"这一个职责
                if (key.Length == 0)
                {
                    continue;
                }

                if (seen.ContainsKey(key))
                {
                    duplicated.Add(seen[key]);
                    continue;
                }

                seen[key] = name.Trim();
                EnsureNoDuplicateValue(name, values);
            }

            if (duplicated.Count > 0)
            {
                throw new CustomException(ResultCode.PARAM_ERROR,
                    $"规格名重复：{string.Join("、", duplicated.Distinct())}。同一商品下规格名必须唯一，请修改后再保存。");
            }
        }

        /// <summary>
        /// 同一规格组内规格值重复：规格值同样是 SKU "名:值" 组合的一部分，
        /// 重复值会生成两条完全相同的规格组合，前台选中后无法区分。
        /// </summary>
        private static void EnsureNoDuplicateValue(string name, List<string> values)
        {
            if (values == null || values.Count <= 1)
            {
                return;
            }

            var duplicates = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .GroupBy(v => v.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new CustomException(ResultCode.PARAM_ERROR,
                    $"规格「{name}」的规格值重复：{string.Join("、", duplicates)}。请修改后再保存。");
            }
        }
    }
}
