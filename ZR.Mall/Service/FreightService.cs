using ZR.Mall.Common;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 运费编排服务：批量查库 → 组装 → 交给纯函数引擎计算
    /// </summary>
    /// <remarks>
    /// C 端试算与下单主流程都通过本服务计算，两条链路不会算出不同的运费。
    /// 本服务只做取数、组装与转异常，不含任何计费规则（规则全在 <see cref="FreightCalculator"/>）。
    /// </remarks>
    [AppService(ServiceType = typeof(IFreightService))]
    public class FreightService : BaseService<FreightTemplate>, IFreightService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IAreaService _areaService;

        public FreightService(IAreaService areaService)
        {
            _areaService = areaService;
        }

        /// <summary>
        /// 按「收货地区 + 商品行」计算运费。
        /// </summary>
        /// <param name="items">商品行（skuId + 数量）</param>
        /// <param name="goodsAmount">商品总额（整单口径，不含运费）</param>
        /// <param name="address">收货地区（名称 + 可选编码）</param>
        /// <returns>计算结果；命中不配送地区时抛异常</returns>
        /// <exception cref="CustomException">收货地区命中任一模板的「不配送地区」</exception>
        public FreightCalcResult CalculateForCheckout(List<FreightCheckoutItem> items,
            decimal goodsAmount, AreaQueryDto address)
        {
            // skuTemplates 在组装阶段填充（引擎只做模板级聚合，摊平后已丢失 SKU 维度），
            // 算出结果后再挂到 result 上，供下单给订单项盖模板快照
            var skuTemplates = new Dictionary<long, FreightTemplate>();
            var request = BuildRequest(items, goodsAmount, address, skuTemplates);
            var result = FreightCalculator.Calculate(request);
            result.SkuTemplates = skuTemplates;

            if (result.UndeliverableRegionNames.Count > 0)
            {
                // 一次告知全部不可配送地区，而不是让用户改地址试一次
                var regions = string.Join("、", result.UndeliverableRegionNames);
                throw new CustomException($"该地区暂不支持配送：{regions}。请更换收货地址或调整购物车中的商品。");
            }

            if (result.NoTemplate > 0)
            {
                logger.Debug($"[运费] {result.NoTemplate} 件商品未绑定运费模板且无默认模板，按 0 运费计：商品总额={goodsAmount}元");
            }

            return result;
        }

        /// <inheritdoc />
        public Dictionary<long, FrontFreightInfoDto> ResolveFrontFreightInfoBatch(
            IEnumerable<(long ProductId, long? FreightTemplateId)> products)
        {
            var result = new Dictionary<long, FrontFreightInfoDto>();
            var rows = products?.ToList();
            if (rows == null || rows.Count == 0)
            {
                return result;
            }

            // ① 一次查完所有被绑定的模板（已被删的查不到，自然回落默认模板）；② 默认模板一次。
            // 两次查询与商品数量无关 —— 列表页逐条查会退化成 N+1，40 个商品就是 80 次往返。
            var boundIds = rows.Where(x => x.FreightTemplateId != null)
                .Select(x => x.FreightTemplateId!.Value)
                .Distinct()
                .ToList();
            var boundMap = boundIds.Count == 0
                ? new Dictionary<long, FreightTemplate>()
                : Queryable().Where(x => boundIds.Contains(x.Id)).ToList().ToDictionary(x => x.Id);
            var defaultTemplate = Queryable().First(x => x.IsDefault == 1);

            foreach (var (productId, templateId) in rows)
            {
                FreightTemplate bound = null;
                if (templateId != null)
                {
                    boundMap.TryGetValue(templateId.Value, out bound);
                }
                result[productId] = ToFrontFreightInfo(bound, defaultTemplate);
            }

            return result;
        }

        /// <summary>
        /// 把「已查到的绑定模板 + 默认模板」折算成包邮信息（回落规则只在 FreightCalculator.PickTemplate）。
        /// </summary>
        private static FrontFreightInfoDto ToFrontFreightInfo(FreightTemplate bound, FreightTemplate defaultTemplate)
            => FreightCalculator.BuildFreightInfo(FreightCalculator.PickTemplate(bound, defaultTemplate));

        /// <summary>
        /// 组装引擎入参：查 sku/商品/模板/排除地区，摊平成 <see cref="FreightCalcRequest"/>。
        /// </summary>
        /// <remarks>
        /// 查询次数恒定 ≤ 4、不随商品行数增长（sku / product / 模板 / 排除地区各一次）；
        /// 地区编码兜底反查走 AreaService.ResolveCodes，不配送匹配与包邮判断才有依据。
        /// </remarks>
        private FreightCalcRequest BuildRequest(List<FreightCheckoutItem> items,
            decimal goodsAmount, AreaQueryDto address,
            Dictionary<long, FreightTemplate> skuTemplates)
        {
            var request = new FreightCalcRequest { GoodsAmount = goodsAmount };
            if (items == null || items.Count == 0)
            {
                return request;
            }

            // ---- ① 批量查 SKU：拿单价校验与重量 ----
            var skuIds = items.Where(x => x != null).Select(x => x.SkuId).Distinct().ToList();
            var skuMap = Context.Queryable<Skus>()
                .Where(x => skuIds.Contains(x.SkuId) && x.IsDelete == 0)
                .ToList()
                .ToDictionary(x => x.SkuId);

            // ---- ② 批量查商品：拿运费模板绑定与形态 ----
            var productIds = skuMap.Values.Select(x => x.ProductId).Distinct().ToList();
            var productMap = Context.Queryable<Product>()
                .Where(x => productIds.Contains(x.ProductId) && x.IsDelete == 0)
                .Select(x => new { x.ProductId, x.ProductType, x.FreightTemplateId })
                .ToList()
                .ToDictionary(x => x.ProductId);

            // ---- 地区解析（含历史数据按名称兜底反查） ----
            // 防 null：IAreaService 是接口调用，实现（或测试桩）返回 null 时不应打崩下单 ——
            // 地区只影响"按地区排除/包邮"，拿不到编码就退化成正常计费，不能因此拦住成交
            var codes = address == null ? null : _areaService.ResolveCodes(address);
            request.ProvinceCode = codes?.ProvinceCode;
            request.CityCode = codes?.CityCode;
            request.DistrictCode = codes?.DistrictCode;

            // ---- ③ 批量查模板：商品绑定的 + 默认模板（一次查完） ----
            // 虚拟商品不参与：它免运费，把它的模板捎进来只是白查
            var templateIds = productMap.Values
                .Where(x => x.ProductType == ProductTypeEnum.Physical && x.FreightTemplateId != null)
                .Select(x => x.FreightTemplateId!.Value)
                .ToList();
            var defaultTemplate = Queryable().First(x => x.IsDefault == 1);
            if (defaultTemplate != null && !templateIds.Contains(defaultTemplate.Id))
            {
                templateIds.Add(defaultTemplate.Id);
            }
            var templateMap = templateIds.Count == 0
                ? new Dictionary<long, FreightTemplate>()
                : Queryable().Where(x => templateIds.Contains(x.Id)).ToList()
                    .ToDictionary(x => x.Id);

            // ---- ④ 批量查不配送地区（整体交给引擎，由引擎按收货编码自行判定命中） ----
            var hitTemplateIds = templateMap.Keys.ToList();
            var regionRows = hitTemplateIds.Count == 0
                ? new List<FreightTemplateExcludeRegion>()
                : Context.Queryable<FreightTemplateExcludeRegion>()
                    .Where(x => hitTemplateIds.Contains(x.TemplateId))
                    .ToList();
            var regionsByTemplate = regionRows
                .GroupBy(x => x.TemplateId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // ---- 摊平成引擎入参 ----
            foreach (var item in items.Where(x => x != null && x.Quantity > 0))
            {
                if (!skuMap.TryGetValue(item.SkuId, out var sku))
                {
                    // SKU 不存在/已删除：交由调用方（建单链路）报"商品不存在"，
                    // 运费这里跳过该行，不擅自改数量也不抛错
                    logger.Warn($"[运费] 商品行 SKU 不存在或已删除，跳过运费计算：SkuId={item.SkuId}");
                    continue;
                }

                var hasProduct = productMap.TryGetValue(sku.ProductId, out var product);

                // 虚拟商品免运费：不产生计费行，也不参与「不配送地区」判定（唯一实现处，页面不要再加形态分支）
                if (hasProduct && product.ProductType == ProductTypeEnum.Virtual)
                {
                    continue;
                }

                // 商品未绑定模板 → 回落到默认模板；两者都没有则 Template 为 null，引擎按 0 运费计
                var boundId = hasProduct ? product.FreightTemplateId : null;
                FreightTemplate template = null;
                if (boundId != null)
                {
                    templateMap.TryGetValue(boundId.Value, out template);
                }
                // 回落规则只在 PickTemplate 里实现（详情页包邮标签共用同一条）
                template = FreightCalculator.PickTemplate(template, defaultTemplate);

                request.Items.Add(new FreightCalcItem
                {
                    SkuId = item.SkuId,
                    Quantity = item.Quantity,
                    Weight = sku.Weight,
                    Template = template,
                    ExcludeRegions = template != null
                        && regionsByTemplate.TryGetValue(template.Id, out var regions)
                            ? regions
                            : new List<FreightTemplateExcludeRegion>()
                });

                // 记下每行命中的模板：引擎只做模板级聚合，摊平后已丢失 SKU 维度，
                // 下单要靠它给订单项盖快照（否则无法反查运费构成）
                if (template != null)
                {
                    skuTemplates[item.SkuId] = template;
                }
            }

            return request;
        }
    }
}
