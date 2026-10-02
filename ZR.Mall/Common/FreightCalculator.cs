using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 运费计算引擎（纯函数，无数据库 / 缓存 / 租户依赖）
    /// </summary>
    /// <remarks>
    /// <para><b>为什么做成纯函数</b>：C 端试算与下单主流程都要算运费，两者必须给出完全一致的数，
    /// 否则用户会看到"试算 8 元、提交变 10 元"。把计算逻辑收敛到无状态纯函数里，
    /// 两条链路调同一个方法，天然一致；且不依赖数据库意味着所有边界（0 重量、0 数量、
    /// 多模板合并、decimal 精度）都能被单元测试直接覆盖，不必起库。</para>
    ///
    /// <para><b>计费规则</b>（P = 该模板命中的总件数，W = 总重量 kg，G = 整单商品总额）：
    /// 按件 —— P ≤ FirstPiece 则 Freight；否则 Freight + ceil((P − FirstPiece) / AddPiece) × Freight。
    /// 按重 —— W ≤ FirstWeight 则 Freight；否则 Freight + ceil((W − FirstWeight) / AddWeight) × Freight。
    /// 包邮 —— FreeShippingThreshold &gt; 0 且 G ≥ 它，则该模板运费 0。
    /// 多模板 —— 各模板分段算出后求和（跨模板 = 分段配送，与主流商城一致）。</para>
    ///
    /// <para><b>精度</b>：全程 decimal，进位用 Math.Ceiling(decimal)，不经过 double，
    /// 避免 0.1+0.2 类误差在进位处被放大成多收或少收一单运费。</para>
    /// </remarks>
    public static class FreightCalculator
    {
        /// <summary>
        /// 计算运费。
        /// </summary>
        /// <param name="request">计算入参；为 null 或无商品行时返回空结果（运费 0）</param>
        /// <returns>计算结果，含合计运费、是否包邮、按模板分段的明细与命中的不配送地区</returns>
        public static FreightCalcResult Calculate(FreightCalcRequest request)
        {
            var result = new FreightCalcResult();
            if (request == null)
            {
                return result;
            }

            result.GoodsAmount = request.GoodsAmount;
            if (request.Items == null || request.Items.Count == 0)
            {
                return result;
            }

            // 先按模板分组：同一模板的多个商品行必须合并成一个包裹算一次运费，
            // 逐行算会把"买 3 件同一模板"收成 3 份首件运费
            var groups = request.Items
                .GroupBy(x => x.Template?.Id ?? 0)
                .Select(g => new
                {
                    Template = g.First().Template,
                    TotalPiece = g.Sum(x => Math.Max(x.Quantity, 0)),
                    TotalWeight = g.Sum(x => Math.Max(x.Quantity, 0) * Math.Max(x.Weight, 0m)),
                    HitRegions = g.Where(x => x.ExcludeRegions != null)
                        .SelectMany(x => x.ExcludeRegions)
                        .ToList()
                })
                .OrderBy(g => g.Template?.Id ?? 0)
                .ToList();

            foreach (var group in groups)
            {
                // 命中不配送地区：引擎自己比对收货编码（而不是要求调用方预过滤）——
                // 预过滤是个脆弱约定，漏一处就会把"未配置的地区"误判成不可配送，直接拦掉订单。
                // 原因待汇总后统一抛出（一次告知全部，避免用户改一个试一次）
                foreach (var region in group.HitRegions)
                {
                    if (!IsRegionHit(region, request))
                    {
                        continue;
                    }
                    var name = string.IsNullOrWhiteSpace(region.AreaName) ? region.AreaCode : region.AreaName;
                    if (!string.IsNullOrWhiteSpace(name) && !result.UndeliverableRegionNames.Contains(name))
                    {
                        result.UndeliverableRegionNames.Add(name);
                    }
                }

                // 未绑定模板：计 0 运费，但仍输出明细便于运营发现"忘记配模板"
                if (group.Template == null)
                {
                    result.NoTemplate += group.TotalPiece;
                    result.Details.Add(new FreightTemplateHit
                    {
                        TotalPiece = group.TotalPiece,
                        TotalWeight = group.TotalWeight,
                        Freight = 0m,
                        NoTemplate = true
                    });
                    continue;
                }

                var template = group.Template;
                var hit = new FreightTemplateHit
                {
                    TemplateId = template.Id,
                    TemplateName = template.TemplateName,
                    TotalPiece = group.TotalPiece,
                    TotalWeight = group.TotalWeight
                };

                // 包邮门槛按整单商品总额比较（含运费前），与 GMV 口径一致；
                // 若按含运费金额比较，会出现"运费把自己送成免邮"的循环论证
                if (template.FreeShippingThreshold > 0 && request.GoodsAmount >= template.FreeShippingThreshold)
                {
                    hit.FreeShipping = true;
                    result.FreeShipping = true;
                }
                else
                {
                    hit.Freight = template.ChargeType == (int)FreightChargeTypeEnum.ByWeight
                        ? CalcByWeight(template, group.TotalWeight)
                        : CalcByPiece(template, group.TotalPiece);
                }

                result.Freight += hit.Freight;
                result.Details.Add(hit);
            }

            return result;
        }

        /// <summary>
        /// 判断某条不配送地区规则是否命中收货地区。
        /// </summary>
        /// <remarks>
        /// 命中 = 排除行的 AreaCode 等于收货的省/市/区**任一级**编码。
        /// 这样运营只存省级编码就排除全省、存市级就排除该市，不必逐级展开配置。
        ///
        /// 收货地区完全解析不出编码时（历史脏数据）**不判定为命中** ——
        /// 宁可放行让用户下单（运费略高），也不能因为一条查不到编码的地址就拦住成交。
        /// </remarks>
        private static bool IsRegionHit(FreightTemplateExcludeRegion region, FreightCalcRequest request)
        {
            if (region == null || string.IsNullOrWhiteSpace(region.AreaCode))
            {
                return false;
            }
            var code = region.AreaCode.Trim();
            return string.Equals(code, request.ProvinceCode?.Trim(), StringComparison.Ordinal)
                || string.Equals(code, request.CityCode?.Trim(), StringComparison.Ordinal)
                || string.Equals(code, request.DistrictCode?.Trim(), StringComparison.Ordinal);
        }

        /// <summary>
        /// 按件计费：首件内一个价，超出部分按续件单位**向上取整**加价。
        /// </summary>
        /// <remarks>
        /// 取整必须向上：不足一续件也按一续件收，否则买 2 件（首件 1、续件 1）会漏收。
        /// 参数非法（续件 ≤ 0）时退化为"首件价 + 每件各加一份"，宁可多收也绝不能因除零
        /// 抛异常把下单链路打断 —— 非法模板由服务层在保存时拦截。
        /// </remarks>
        private static decimal CalcByPiece(FreightTemplate template, int totalPiece)
        {
            if (totalPiece <= 0)
            {
                return 0m;
            }

            var first = Math.Max(template.FirstPiece, 0m);
            if (totalPiece <= first)
            {
                return template.Freight;
            }

            var extraUnits = template.AddPiece > 0
                ? (int)Math.Ceiling((totalPiece - first) / template.AddPiece)
                : totalPiece - (int)first;

            // 首件为 0 时不存在"首件"这一段，不能再额外收一份首件价 ——
            // 否则 1 件商品会被收成"首件 10 + 续件 10"= 20，比配置意图多收一倍
            var baseFreight = first > 0 ? template.Freight : 0m;
            return baseFreight + extraUnits * template.Freight;
        }

        /// <summary>
        /// 按重计费：首重（kg）内一个价，超出部分按续重单位**向上取整**加价。
        /// </summary>
        /// <remarks>
        /// 重量为 0（SKU 未填重量）时整组按首重价计 —— 收一份首重运费而不是 0，
        /// 否则"不填重量就不收运费"会变成一个可被利用的漏洞。
        /// </remarks>
        private static decimal CalcByWeight(FreightTemplate template, decimal totalWeight)
        {
            var first = Math.Max(template.FirstWeight, 0m);
            if (totalWeight <= first)
            {
                return template.Freight;
            }

            // 续重 ≤ 0 属非法参数：无法按单位切分，退化为只加一份续重（宁可多收也别除零）
            var extraUnits = template.AddWeight > 0
                ? (int)Math.Ceiling((totalWeight - first) / template.AddWeight)
                : 1;

            // 与按件同理：首重为 0 时不再额外收首重价，避免 1 件商品被收成两份
            var baseFreight = first > 0 ? template.Freight : 0m;
            return baseFreight + extraUnits * template.Freight;
        }
    }
}
