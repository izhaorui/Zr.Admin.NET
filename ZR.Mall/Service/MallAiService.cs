using Infrastructure.AI;
using Infrastructure.Helper;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;
using ZR.Model.AI;
using Stj = System.Text.Json;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 商城 AI 服务：商品营销文案生成。
    ///
    /// 与系统 AI / 工作流 AI 共用同一套基建（<see cref="AiHelper"/> 负责开关校验、提示词加载、
    /// 单次问答与重试；<see cref="AiSceneCatalog.ShopProductCopy"/> 参与治理记账）。
    /// 只做「读商品 → 组装素材 → 调模型 → 解析 JSON → 返回草稿」，<b>不落库</b>，
    /// 采纳与否由用户在商品编辑页自行决定。
    /// </summary>
    [AppService(ServiceType = typeof(IMallAiService), ServiceLifetime = LifeTime.Transient)]
    public class MallAiService : IMallAiService
    {
        /// <summary>拼进提示词的 SKU 上限：规格很多的商品只取前 N 条，避免撑爆上下文</summary>
        private const int MaxSkuInPrompt = 20;

        /// <summary>单个文本字段截断长度（商品简介/规格简介等）</summary>
        private const int MaxTextFieldLength = 500;

        private static readonly Stj.JsonSerializerOptions MetricJsonOptions = new()
        {
            PropertyNamingPolicy = Stj.JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly IProductService _productService;
        private readonly ISkusService _skusService;
        private readonly ICategoryService _categoryService;
        private readonly IBrandService _brandService;

        public MallAiService(
            IProductService productService,
            ISkusService skusService,
            ICategoryService categoryService,
            IBrandService brandService)
        {
            _productService = productService;
            _skusService = skusService;
            _categoryService = categoryService;
            _brandService = brandService;
        }

        /// <inheritdoc/>
        public async Task<MallProductCopyResult> GenerateProductCopyAsync(MallProductCopyInput input)
        {
            if (input == null || input.ProductId <= 0)
            {
                throw new CustomException("请先选择商品");
            }

            // 读实体原始字段（Unit/OriginalPrice 等仅在 Product 上，ProductDto 未承载）
            var product = _productService.Queryable()
                .Where(p => p.ProductId == input.ProductId)
                .First();
            if (product == null)
            {
                throw new CustomException("商品不存在");
            }

            var skus = _skusService.GetSkus(product.ProductId);
            var material = BuildMaterial(product, skus, input);
            var user = Stj.JsonSerializer.Serialize(material, MetricJsonOptions);

            var reply = await AiHelper.ChatSafeAsync(
                AiHelper.GetPromptOrThrow("mall/product-copy.md", "商品文案生成"),
                user,
                AiSceneCatalog.ShopProductCopy).ConfigureAwait(false);

            return ParseResult(reply);
        }

        /// <summary>
        /// 组装给模型的商品素材：只传生成文案所需字段，敏感/无关字段（图片地址、审计字段）不传。
        /// 「用户补充」（关键词/语气/人群/语言）与商品事实分开承载，避免模型把用户输入当成商品属性。
        /// </summary>
        private object BuildMaterial(Product product, List<Skus> skus, MallProductCopyInput input)
        {
            var categoryName = product.CategoryId is int cid && cid > 0
                ? _categoryService.Queryable().Where(c => c.CategoryId == cid).Select(c => c.Name).First()
                : null;
            var brandName = product.BrandId is long bid && bid > 0
                ? _brandService.Queryable().Where(b => b.Id == bid).Select(b => b.Name).First()
                : null;

            var skuItems = (skus ?? new List<Skus>())
                .Where(s => s != null && s.IsDelete == 0)
                .OrderBy(s => s.SortId)
                .Take(MaxSkuInPrompt)
                .Select(s => new
                {
                    spec = AiHelper.Truncate(s.SpecCombination, 100),
                    price = s.Price,
                    stock = s.Stock
                })
                .ToList();

            return new
            {
                product = new
                {
                    name = product.ProductName,
                    category = categoryName,
                    brand = brandName,
                    unit = product.Unit,
                    price = product.Price,
                    maxPrice = product.MaxPrice,
                    originalPrice = product.OriginalPrice,
                    productType = product.ProductType.ToString(),
                    specSummary = AiHelper.Truncate(product.SpecSummary, MaxTextFieldLength),
                    introduce = AiHelper.Truncate(product.Introduce, MaxTextFieldLength),
                    skus = skuItems
                },
                userHints = new
                {
                    keywords = AiHelper.Truncate(input.Keywords, MaxTextFieldLength),
                    tone = AiHelper.Truncate(input.Tone, 50),
                    targetAudience = AiHelper.Truncate(input.TargetAudience, 50),
                    language = AiHelper.Truncate(input.Language, 20)
                }
            };
        }

        /// <summary>
        /// 解析模型返回的 JSON 文案。非 JSON 时把原文放进 Raw 由前端提示重试，不伪造结构化结果。
        /// </summary>
        private static MallProductCopyResult ParseResult(string raw)
        {
            var result = new MallProductCopyResult();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var json = JsonHelper.StripMarkdown(raw);
            try
            {
                using var doc = Stj.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != Stj.JsonValueKind.Object)
                {
                    result.Raw = raw.Trim();
                    return result;
                }

                result.Title = (ReadString(root, "title") ?? string.Empty).Trim();
                result.Description = (ReadString(root, "description") ?? string.Empty).Trim();
                result.SellingPoints = ReadStringArray(root, "sellingPoints");
                result.SeoKeywords = ReadStringArray(root, "seoKeywords");
            }
            catch (Stj.JsonException)
            {
                result.Raw = raw.Trim();
            }
            return result;
        }

        private static string ReadString(Stj.JsonElement el, string name)
        {
            return el.TryGetProperty(name, out var v) && v.ValueKind == Stj.JsonValueKind.String
                ? v.GetString()
                : null;
        }

        /// <summary>读取字符串数组；兼容模型偶尔返回「单个字符串」而非数组的情况。</summary>
        private static List<string> ReadStringArray(Stj.JsonElement el, string name)
        {
            var list = new List<string>();
            if (!el.TryGetProperty(name, out var arr))
            {
                return list;
            }

            if (arr.ValueKind == Stj.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind == Stj.JsonValueKind.String)
                    {
                        var text = item.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            list.Add(text);
                        }
                    }
                }
            }
            else if (arr.ValueKind == Stj.JsonValueKind.String)
            {
                var text = arr.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    list.Add(text);
                }
            }
            return list;
        }
    }
}
