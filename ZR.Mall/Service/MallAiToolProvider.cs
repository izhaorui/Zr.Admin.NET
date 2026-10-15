using Infrastructure.Attribute;
using Newtonsoft.Json.Linq;
using ZR.Mall.Enum;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// AI 助手扩展工具：商城运营查询（`mall_` 前缀）。
    ///
    /// 通过 <see cref="IAiAssistantToolProvider"/> 向全局 AI 助手注册，工具内部调用商城既有 Service，
    /// 天然继承数据范围与租户隔离。工具只返回**聚合结论**（库存/上下架状态、订单概况），
    /// <b>不返回订单明细、买家手机号、收货地址等敏感字段</b>。
    ///
    /// 权限：声明 Permission 后，无权用户在前端工具清单中看不到对应工具；
    /// 执行入口仍以当前登录用户身份调用既有 Service。
    /// </summary>
    [AppService(ServiceType = typeof(IAiAssistantToolProvider), ServiceLifetime = LifeTime.Transient)]
    public class MallAiToolProvider : IAiAssistantToolProvider
    {
        private const string ToolProductStock = "mall_product_stock";
        private const string ToolRecentOrders = "mall_recent_orders";

        /// <summary>单次返回的商品条数上限</summary>
        private const int MaxProductLimit = 20;

        /// <summary>每个商品最多展示的 SKU 条数</summary>
        private const int MaxSkuPerProduct = 8;

        /// <summary>订单概况可统计的最大天数</summary>
        private const int MaxOrderDays = 90;

        private readonly IProductService _productService;
        private readonly IOMSOrderService _orderService;

        public string ProviderName => "mall";

        public MallAiToolProvider(IProductService productService, IOMSOrderService orderService)
        {
            _productService = productService;
            _orderService = orderService;
        }

        public List<AiToolDef> GetToolDefs()
        {
            return new List<AiToolDef>
            {
                new AiToolDef
                {
                    Name = ToolProductStock,
                    Label = "商品库存查询",
                    Permission = "shop:product:list",
                    Samples = new List<string>
                    {
                        "帮我查一下「蓝牙耳机」的库存和上下架状态",
                        "商品「XXXX」还有货吗？是在售还是下架了？"
                    },
                    Description = "按商品名关键词查询商城商品的库存与上下架状态（含各规格库存/价格）。"
                        + "适用于「XX商品还有库存吗」「XX 是在售还是下架了」「查商品库存」等提问。"
                        + "只返回商品名/上下架状态/库存/规格，不含订单或买家信息。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["keyword"] = new { type = "string", description = "商品名关键词（必填），支持商品名部分匹配" },
                            ["limit"] = new { type = "integer", description = "最多返回商品数，默认 10，最大 20" }
                        },
                        required = new[] { "keyword" }
                    }
                },
                new AiToolDef
                {
                    Name = ToolRecentOrders,
                    Label = "近期订单概况",
                    Permission = "oms:order:list",
                    Samples = new List<string>
                    {
                        "看一下最近 7 天的订单情况",
                        "最近 30 天卖了多少？退款多吗？"
                    },
                    Description = "查询近期订单的**聚合概况**（有效订单数、销售额、实收、客单价、退款、售后单数），"
                        + "按支付时间口径、时间窗内统计，不返回任何订单明细或买家信息。"
                        + "适用于「最近N天订单情况/卖了多少/退款率」等提问。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["days"] = new { type = "integer", description = "统计最近多少天（按支付时间），默认 7，最大 90" }
                        },
                        required = Array.Empty<string>()
                    }
                }
            };
        }

        public async Task<AiToolExecResult> ExecuteAsync(string toolName, string argsJson, long userId)
        {
            switch (toolName)
            {
                case ToolProductStock:
                    return QueryProductStock(ParseArgs(argsJson));
                case ToolRecentOrders:
                    return await QueryRecentOrdersAsync(ParseArgs(argsJson));
                default:
                    return null;
            }
        }

        private static JObject ParseArgs(string argsJson)
        {
            if (string.IsNullOrWhiteSpace(argsJson))
            {
                return new JObject();
            }
            try
            {
                return JObject.Parse(argsJson);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return new JObject();
            }
        }

        /// <summary>查询商品库存/上下架状态（只读，按关键词匹配商品名）。</summary>
        private AiToolExecResult QueryProductStock(JObject args)
        {
            var keyword = args["keyword"]?.Value<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return AiToolExecResult.Error("请提供要查询的商品名关键词，例如「蓝牙耳机」。");
            }

            var limit = args["limit"]?.Value<int>() ?? 10;
            if (limit < 1) limit = 1;
            if (limit > MaxProductLimit) limit = MaxProductLimit;

            var page = _productService.GetList(new ShoppingProductQueryDto
            {
                ProductName = keyword,
                PageNum = 1,
                PageSize = limit
            });

            var list = page?.Result;
            if (list == null || list.Count == 0)
            {
                return AiToolExecResult.Success($"未找到名称包含「{keyword}」的商品。可换个关键词再试。");
            }

            var lines = new List<string>();
            foreach (var p in list)
            {
                var status = p.SaleStatus == SaleStatus.OnSale ? "在售" : "已下架";
                var skuText = BuildSkuSummary(p);
                lines.Add($"- {p.ProductName}（{status}）：总库存 {p.Stock}{skuText}");
            }

            var more = page.TotalNum > list.Count ? $"，仅显示前 {list.Count} 条（共 {page.TotalNum} 条）" : string.Empty;
            return AiToolExecResult.Success(
                $"按关键词「{keyword}」查询到 {page.TotalNum} 个商品{more}：\n" + string.Join("\n", lines));
        }

        private static string BuildSkuSummary(ProductDto product)
        {
            var skus = product.Skus;
            if (skus == null || skus.Count == 0)
            {
                return string.Empty;
            }

            var parts = skus
                .Where(s => s != null)
                .OrderBy(s => s.SortId)
                .Take(MaxSkuPerProduct)
                .Select(s =>
                {
                    var spec = string.IsNullOrWhiteSpace(s.SpecCombination) ? "默认" : s.SpecCombination;
                    return $"{spec}(库存 {s.Stock}，￥{s.Price})";
                })
                .ToList();
            if (parts.Count == 0)
            {
                return string.Empty;
            }

            var suffix = skus.Count > parts.Count ? $" 等 {skus.Count} 个规格" : string.Empty;
            return $"\n    规格：{string.Join("；", parts)}{suffix}";
        }

        /// <summary>查询近期订单聚合概况（无明细，仅统计值）。</summary>
        private async Task<AiToolExecResult> QueryRecentOrdersAsync(JObject args)
        {
            var days = args["days"]?.Value<int>() ?? 7;
            if (days < 1) days = 1;
            if (days > MaxOrderDays) days = MaxOrderDays;

            var end = DateTime.Now;
            var begin = end.Date.AddDays(1 - days);
            var dto = new OMSOrderQueryDto
            {
                BeginCreateTime = begin,
                EndCreateTime = end
            };

            // 两条聚合查询：销售总额/订单数 + 经营概览（客单价/退款/售后），口径统一按支付时间。
            var sales = await _orderService.GetTotalSales(dto);
            var overview = await _orderService.GetBusinessOverview(dto);

            if (overview.OrderCount == 0 && sales.OrderCount == 0)
            {
                return AiToolExecResult.Success(
                    $"最近 {days} 天（{begin:yyyy-MM-dd} 至 {end:yyyy-MM-dd}）没有已支付的订单。");
            }

            var lines = new List<string>
            {
                $"- 有效订单数：{overview.OrderCount}",
                $"- 销售额（商品口径，不含运费）：￥{sales.TotalSales:0.##}",
                $"- 实收金额（含运费）：￥{overview.TotalPaid:0.##}",
                $"- 客单价：￥{overview.AvgOrderAmount:0.##}",
                $"- 退款订单数：{overview.RefundOrderCount}，退款金额：￥{overview.RefundAmount:0.##}，金额退款率：{overview.RefundAmountRate:P2}",
                $"- 售后申请单数：{overview.AfterSaleCount}"
            };

            return AiToolExecResult.Success(
                $"最近 {days} 天订单概况（{begin:yyyy-MM-dd} 至 {end:yyyy-MM-dd}，按支付时间口径，仅聚合数据）：\n"
                + string.Join("\n", lines));
        }
    }
}
