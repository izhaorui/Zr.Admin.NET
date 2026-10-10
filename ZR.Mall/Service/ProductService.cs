using ZR.Common;
using ZR.Mall.Common;
using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 商品管理Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IProductService))]
    public class ProductService : BaseService<Product>, IProductService
    {
        private readonly ISkusService skusService;
        private readonly IProductSpecService specService;
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 单次「取下单表单定义」允许的 SKU 上限（见 <see cref="GetFrontFormFields"/>）。
        /// </summary>
        private const int MaxFormFieldSkuQuery = 50;

        /// <summary>
        /// 列表分页的每页上限（后台与 C 端共用）。C 端匿名可访问，必须兜住超大 PageSize 的拖库风险。
        /// 导出接口不受此上限约束（按需一次性取全量，见 ExportList）。
        /// </summary>
        private const int MaxListPageSize = 100;

        /// <summary>
        /// 商品名全文索引名（MySQL 就绪探测用；SQL Server 索引名由系统生成，探测只看该表有没有全文索引）。
        /// 与 document/doc 里的建索引脚本保持一致。
        /// </summary>
        private const string ProductNameFtsIndex = "ft_product_name";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="skusService"></param>
        /// <param name="shoppingProductSpecService"></param>
        public ProductService(ISkusService skusService, IProductSpecService shoppingProductSpecService)
        {
            this.skusService = skusService;
            this.specService = shoppingProductSpecService;
            // 不再固定 MallDb：同 OMSOrderService，恢复 BaseService 默认按租户路由。
        }

        /// <summary>
        /// 查询商品管理列表（后台，不缓存、不套 C 端排序规则）
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ProductDto> GetList(ShoppingProductQueryDto parm)
        {
            ClampPaging(parm);
            return QueryList(parm, useCache: false, isFrontList: false);
        }

        /// <summary>
        /// C 端商品列表。只让「首页默认列表」进查询缓存：无任何筛选条件 + 第 1 页 + 默认综合档。
        /// 这类查询参数固定、重复率最高；带筛选/翻页/换档位的组合无限，缓存键会随之膨胀，故不缓存。
        ///
        /// 说明：SaleStatus 由 C 端 Controller 强制为「在售」，是常量，不参与「是否默认查询」的判定，
        /// 否则条件永远不成立、缓存形同虚设。
        /// </summary>
        public PagedInfo<ProductDto> GetFrontList(ShoppingProductQueryDto parm)
        {
            ClampPaging(parm);

            var isDefaultList = parm.PageNum <= 1
                && string.IsNullOrWhiteSpace(parm.ProductName)
                && string.IsNullOrWhiteSpace(parm.ProductCode)
                && string.IsNullOrWhiteSpace(parm.Keyword)
                && !parm.OnlyInStock
                && parm.ProductId == null
                && parm.CategoryId == null
                && parm.BrandId == null
                && parm.BeginAddTime == null
                && parm.EndAddTime == null
                // 仅默认「综合」档进缓存：其余档位排序键不同，虽缓存键天然不同，但仍只缓存默认列表，
                // 避免任前端自由传 sortMode 时把缓存键撑爆。
                && ProductSortModeResolver.Resolve(parm.SortMode) == ProductSortMode.Synthesize;

            return QueryList(parm, useCache: isDefaultList, isFrontList: true);
        }

        /// <summary>
        /// 分页参数兜底：PageNum/PageSize 越界修正，PageSize 上限 <see cref="MaxListPageSize"/>。
        /// 直接改传入对象（请求级对象，调用方在此之后不再使用旧值）。
        /// </summary>
        private static void ClampPaging(ShoppingProductQueryDto parm)
        {
            if (parm == null) return;
            if (parm.PageNum < 1) parm.PageNum = 1;
            if (parm.PageSize < 1) parm.PageSize = 20;              // 与 PagerInfo 默认一致
            else if (parm.PageSize > MaxListPageSize) parm.PageSize = MaxListPageSize;
        }

        /// <summary>
        /// 转义 LIKE 关键词中的通配符（%、_、[），使其按**字面**匹配而不是当通配符用。
        /// SqlSugar 的 Contains 已参数化（无注入风险），这里只解决"用户输入 % 却匹配到全部"的通配语义。
        /// 按库方言处理：SqlServer 用方括号转义；MySql/PgSql 默认转义符为反斜杠；
        /// 其余库无默认转义符，退化为剔除通配符，至少不让其生效。
        /// </summary>
        private string EscapeLikeKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return null;
            var kw = keyword.Trim();

            return Context.CurrentConnectionConfig.DbType switch
            {
                SqlSugar.DbType.SqlServer => kw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]"),
                SqlSugar.DbType.MySql or SqlSugar.DbType.PostgreSQL => kw.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_"),
                _ => kw.Replace("%", string.Empty).Replace("_", string.Empty).Replace("[", string.Empty),
            };
        }

        /// <summary>
        /// 商品列表查询主体：后台走 GetList（不缓存、不排序规则），C 端走 GetFrontList（默认列表缓存 + 前台排序）。
        ///
        /// 缓存注意：本查询 Includes(Skus) 会 JOIN mms_skus，下单扣库存与 SKU 维护都会触发
        /// IsAutoRemoveDataCache 自动清缓存 —— 好处是库存/销量不会被缓存拖旧（成交后即刷新），
        /// 代价是命中率受写入频率影响。TTL 60 秒仅作兜底。
        /// </summary>
        /// <param name="parm">查询参数（含分页与筛选）</param>
        /// <param name="useCache">是否启用查询缓存（仅 C 端默认列表为 true）</param>
        /// <param name="isFrontList">是否 C 端列表：为 true 时套用前台排序规则（见 ApplyFrontSorting）。</param>
        private PagedInfo<ProductDto> QueryList(ShoppingProductQueryDto parm, bool useCache, bool isFrontList)
        {
            // C 端关键词检索：走全文索引（未就绪自动回落 LIKE），只搜商品名，见 BuildProductSearchCondition
            var (searchSql, searchParams) = BuildProductSearchCondition(parm.Keyword?.Trim());
            var predicate = QueryExp(parm);

            var query = Queryable()
                .WithCacheIF(useCache, 60)
                .Where(predicate.ToExpression())
                // 关键词条件用参数化 raw SQL：全文谓词（CONTAINS / MATCH...AGAINST）表达式无法表达，
                // 同时也避免关键词被 SqlSugar 内联成 SQL 字面量。
                .WhereIF(!string.IsNullOrEmpty(searchSql), searchSql, searchParams)
                .Includes(x => x.Skus.Where(f => f.IsDelete == 0).ToList())
                .Includes(x => x.Category)
                .Includes(x => x.Brand);

            if (isFrontList)
            {
                // 档位自 SortMode 解析（空值/非法值回落综合）。
                query = ApplyFrontSorting(query, parm, ProductSortModeResolver.Resolve(parm.SortMode));
                // 清掉通用排序字段：ToPage 内还会按 parm.Sort 再排一次，叠加会造成主/次排序错位；
                // 且 parm.Sort 是前端可控字符串，清空同时收敛其排序注入面。
                parm.Sort = string.Empty;
            }

            var response = query.ToPage<Product, ProductDto>(parm);
            foreach (var item in response.Result)
            {
                item.Stock = item.Skus.Sum(s => s.Stock);
                item.TotalSalesVolume = item.Skus.Sum(s => s.SalesVolume);
            }
            return response;
        }

        /// <summary>
        /// C 端排序规则（全部在库内完成，保证分页稳定）：
        /// <list type="bullet">
        /// <item>缺货沉底（所有档位）：有货的排前面，全缺货的排末尾；缺货商品默认不隐藏
        /// （要隐藏由 <see cref="ShoppingProductQueryDto.OnlyInStock"/> 在 WHERE 层过滤）。</item>
        /// <item>综合（默认）：分场景权重 → 有效置顶 SortId → 时间衰减热度分 → 上新时间。
        /// （关键词已收敛为"仅商品名"且由 WHERE 的全文谓词过滤，命中集内不再做字段相关性分级）</item>
        /// <item>销量 / 价格升 / 价格降 / 上新：只按各自字段排序，不带置顶与场景权重 ——
        /// 用户明确选了硬排序，就不该被运营置顶插队。</item>
        /// <item>所有档位统一以 ProductId DESC 兜底，保证同值时分页不跳页、不重复。</item>
        /// </list>
        ///
        /// 销量与库存都读 <c>Product</c> 上由 <see cref="Common.ProductTotalsRefresher"/> 维护的冗余列
        /// （TotalSalesVolume / TotalStock），排序全程不出现相关子查询 —— 否则 SqlSugar 在 Includes + 分页下
        /// 无法绑定子查询里的外表别名（"could not be bound"）。
        /// 置顶时效 / 热度分都用 SqlFunc 表达式在库内算，避免把结果集拉进内存再排。
        /// </summary>
        /// <param name="query">待排序的查询</param>
        /// <param name="parm">查询参数（用于场景推断与分类判定）</param>
        /// <param name="mode">排序档位</param>
        private static ISugarQueryable<Product> ApplyFrontSorting(ISugarQueryable<Product> query, ShoppingProductQueryDto parm, ProductSortMode mode)
        {
            // 缺货沉底：有货(TotalStock>0)排前、全缺货排末尾。
            // 读维护好的冗余列，而不是 SKU 相关子查询 —— SqlSugar 在 Includes + 分页场景下不会回写
            // ORDER BY 子查询里的外表别名，用子查询会报 "multi-part identifier p.ProductId could not be bound"。
            query = query.OrderBy(p => SqlFunc.IIF(p.TotalStock > 0, 1, 0), OrderByType.Desc);

            if (mode == ProductSortMode.Synthesize)
            {
                // 分场景权重：按请求推断的场景取对应列（首页/分类/搜索）
                switch (ResolveScene(parm))
                {
                    case ProductScene.Category:
                        query = query.OrderBy(p => p.CategoryWeight, OrderByType.Desc);
                        break;
                    case ProductScene.Search:
                        query = query.OrderBy(p => p.SearchWeight, OrderByType.Desc);
                        break;
                    default:
                        query = query.OrderBy(p => p.HomeWeight, OrderByType.Desc);
                        break;
                }

                // 置顶时效：只有当前时间落在 [TopBeginTime, TopEndTime] 窗口内，SortId 才参与排序；
                // 窗口外按 0（等于不置顶），实现"过期自动失效"。
                var now = DateTime.Now;
                query = query.OrderBy(p => SqlFunc.IIF(
                    (p.TopBeginTime == null || p.TopBeginTime <= now) && (p.TopEndTime == null || p.TopEndTime >= now),
                    p.SortId, 0), OrderByType.Desc);

                // 时间衰减热度分：销量越高越热、上架越久衰减越多。用 (销量+1)/(上架天数+10) 平滑，避免除零与全新商品霸榜
                query = query.OrderBy(p => (p.TotalSalesVolume + 1) * 1.0
                        / (SqlFunc.DateDiff(DateType.Day, p.AddTime ?? now, now) + 10), OrderByType.Desc)
                    .OrderBy(p => p.AddTime, OrderByType.Desc);
            }
            else if (mode == ProductSortMode.Sales)
            {
                query = query.OrderBy(p => p.TotalSalesVolume, OrderByType.Desc);
            }
            else if (mode == ProductSortMode.PriceAsc)
            {
                query = query.OrderBy(p => p.Price, OrderByType.Asc);
            }
            else if (mode == ProductSortMode.PriceDesc)
            {
                query = query.OrderBy(p => p.Price, OrderByType.Desc);
            }
            else
            {
                query = query.OrderBy(p => p.AddTime, OrderByType.Desc);
            }

            return query.OrderBy(p => p.ProductId, OrderByType.Desc);
        }

        /// <summary>
        /// 推断 C 端展示场景：带关键词=搜索；带分类=分类；其余=首页/推荐位。决定综合档用哪一列场景权重。
        /// </summary>
        private static ProductScene ResolveScene(ShoppingProductQueryDto parm)
        {
            if (!string.IsNullOrWhiteSpace(parm.Keyword)) return ProductScene.Search;
            if (parm.CategoryId != null) return ProductScene.Category;
            return ProductScene.Home;
        }

        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="ProductId"></param>
        /// <returns>商品详情；商品不存在时返回 <c>null</c>，由调用方决定返回什么错误</returns>
        public ProductDto GetInfo(long ProductId)
        {
            var list = Queryable()
                .Where(x => x.ProductId == ProductId)
                .Take(1)
                .ToList();
            if (list.Count == 0)
            {
                return null;
            }

            var info = list[0].Adapt<ProductDto>();

            var spec = specService.GetList(f => f.ProductId == info.ProductId);
            var skus = skusService.GetSkus(info.ProductId);

            info.Spec = spec.Adapt<List<ProductSpecDto>>();
            info.Skus = skus.ToList().OrderBy(f => f.SortId).Adapt<List<SkusDto>>();

            return info;
        }

        /// <summary>
        /// 添加商品管理
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public Product AddShoppingProduct(ProductDto dto)
        {
            // 规格名校验必须放在 UseTran 之前：UseTran 会把异常吞成 result.ErrorMessage，
            // 之后统一抛"添加失败"，用户看不到具体原因。详见 ProductSpecValidator。
            ProductSpecValidator.Validate(dto.Spec);
            // 表单字段定义同理：写坏的正则/重复字段键要在这里拦，否则症状要到买家下单才暴露。
            ProductFormValidator.ValidateDefinition(dto.FormItems);

            var model = dto.Adapt<Product>();
            var result = UseTran(() =>
            {
                model = InsertReturnEntity(model) ?? throw new Exception("添加商品失败");

                if (dto.Skus != null)
                {
                    dto.Skus.ForEach(it =>
                    {
                        it.ProductId = model.ProductId;
                        it.SpecCombination = ProductSpecFormatter.BuildCombination(it.Specs);
                    });

                    skusService.Insert(dto.Skus.Adapt<List<Skus>>());
                }
                //插入商品规格
                dto.Spec.ForEach(x => x.ProductId = model.ProductId);
                specService.InsertRange(dto.Spec.Adapt<List<ProductSpec>>());

                // 价格区间与规格摘要统一由 SKU 侧回写（与 SKU 管理页共用同一实现）：
                // 必须在 SKU 与规格定义都落库之后，否则算出来的是空的。
                skusService.RefreshProductSummary(model.ProductId);
            });
            if (result.IsSuccess)
            {
                return model;
            }
            throw new CustomException("添加失败");
        }

        /// <summary>
        /// 修改商品管理
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public Product UpdateShoppingProduct(ProductDto dto)
        {
            // 同上：校验必须在 UseTran 之前，否则错误信息会被吞成笼统的"修改失败"
            ProductSpecValidator.Validate(dto.Spec);
            ProductFormValidator.ValidateDefinition(dto.FormItems);

            var model = dto.Adapt<Product>();
            // 表单字段未提交时保留库里的配置，不能照直写 NULL：
            // 后台表单设计器尚未上线，老页面保存商品时不带 formItems，一写就静默清空已配的字段，
            // 现场毫无提示（要等下次下单收不到填写值才被发现）。前端带上该字段后即为正常覆盖。
            if (dto.FormItems == null)
            {
                model.FormItems = Queryable().First(p => p.ProductId == model.ProductId)?.FormItems;
            }
            var result = UseTran(() =>
            {
                model.Skus.ForEach(f => f.ProductId = model.ProductId);
                // 字段白名单更新：只允许改"商品业务字段"。
                // 原实现 Update(model, true, ...) 是按非空字段整行更新，请求方传 IsDelete/SalesVolume/
                // TotalSalesVolume/AddTime 等就能直接改删除标记与销量统计 —— 属于越权改账。
                // Price/MaxPrice/SpecSummary 不在此列：由下方 RefreshProductSummary 依据 SKU 重新回写。
                // 用 Context 直连是为保留原链路上的差异日志（EnableDiffLogEvent("修改商品")），
                // BaseService 的字段白名单重载不带差异日志。
                Context.Updateable(model)
                    .UpdateColumns(t => new
                    {
                        t.SortId,
                        // 置顶时效 + 分场景权重：漏加会导致后台配了不生效（且极难察觉），必须一起更新
                        t.TopBeginTime,
                        t.TopEndTime,
                        t.HomeWeight,
                        t.CategoryWeight,
                        t.SearchWeight,
                        t.ProductName,
                        t.ProductCode,
                        t.MainImage,
                        t.ImageUrls,
                        t.VideoUrl,
                        t.SaleStatus,
                        // 形态：漏加会导致"后台改成虚拟但保存不生效"，且要到下单流程才显形
                        t.ProductType,
                        // 下单表单：同理漏加会让后台配的字段静默丢失（配了却收不到买家填写值）
                        t.FormItems,
                        t.CategoryId,
                        t.BrandId,
                        // 运费模板绑定：漏加会导致后台选了模板但保存不生效，
                        // 而运费是下单时才用到的问题，极难被察觉
                        t.FreightTemplateId,
                        t.Introduce,
                        t.Unit,
                        t.OriginalPrice,
                        t.DetailsHtml,
                        t.SpecType,
                        t.PurchaseLimit
                    })
                    .IgnoreColumns(true)
                    .EnableDiffLogEvent("修改商品")
                    .ExecuteCommand();

                var skus = skusService.GetSkus(model.ProductId);

                // 删除旧的规格
                //specService.DeleteSpecByProductId(model.ProductId);
                // 插入新的规格
                dto.Spec.ForEach(f => f.ProductId = model.ProductId);
                //specService.InsertRange(dto.Spec.Adapt<List<ProductSpec>>());
                specService.UpdateProductSpec(model.ProductId, dto.Spec.Adapt<List<ProductSpec>>());
                
                var insertSkus = model.Skus.Where(f => !skus.Any(s => s.SkuId == f.SkuId)).ToList();
                var updateSkus = model.Skus.Where(f => skus.Any(s => s.SkuId == f.SkuId)).ToList();
                var deleteSkus = skus.Where(f => !model.Skus.Any(s => s.SkuId == f.SkuId)).ToList();

                // 软删除sku
                if (deleteSkus.Count > 0)
                {
                    skusService.Deleteable()
                    .Where(f => f.IsDelete == 0)
                    .In(deleteSkus.Select(f => f.SkuId))
                    .IsLogic()
                    .ExecuteCommand();
                    logger.Info($"删除的skuid={deleteSkus.Select(f => f.SkuId)}");
                }

                //生成规格组合快照
                foreach (var item in insertSkus)
                {
                    item.SpecCombination = ProductSpecFormatter.BuildCombination(item.Specs);
                }

                // 插入新的sku
                skusService.Insert(insertSkus);
                // 更新现有 SKU：只改展示/价格/规格。库存仅在本次提交与库内不一致时写入（商家主动改库存）。
                // 不写 LockStock / SalesVolume / Version / IsDelete —— 这些是下单副作用，
                // 整行更新会把锁定库存和销量写成 0，或用打开页面时的库存盖掉期间的扣减。
                foreach (var item in updateSkus)
                {
                    item.ProductId = model.ProductId;
                    item.SpecCombination = ProductSpecFormatter.BuildCombination(item.Specs);
                    var original = skus.First(s => s.SkuId == item.SkuId);
                    if (item.Stock != original.Stock)
                    {
                        Context.Updateable(item)
                            .UpdateColumns(s => new { s.Price, s.Stock, s.ImageUrl, s.SpecCombination, s.Specs, s.SpecIds, s.Weight, s.SortId })
                            .EnableDiffLogEvent("修改商品sku")
                            .ExecuteCommand();
                    }
                    else
                    {
                        Context.Updateable(item)
                            .UpdateColumns(s => new { s.Price, s.ImageUrl, s.SpecCombination, s.Specs, s.SpecIds, s.Weight, s.SortId })
                            .EnableDiffLogEvent("修改商品sku")
                            .ExecuteCommand();
                    }
                }

                // 价格区间与规格摘要统一由 SKU 侧回写：本次的新增、修改与软删除都会在重算中体现
                skusService.RefreshProductSummary(model.ProductId);
            });
            if (!result.IsSuccess)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "修改失败", result.ErrorMessage);
            }
            return model;
        }

        /// <summary>
        /// 修改商品管理
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public long UpdateInfo(ProductDto dto)
        {
            var model = dto.Adapt<Product>();

            var result = Update(model, t => new
            {
                t.SortId,
                // 置顶时效 + 分场景权重：与 UpdateShoppingProduct 保持一致，避免一处能改一处丢失
                t.TopBeginTime,
                t.TopEndTime,
                t.HomeWeight,
                t.CategoryWeight,
                t.SearchWeight,
                t.ProductName,
                t.MainImage,
                t.ImageUrls,
                t.SaleStatus,
                // 形态与运费模板都要一起更新：漏一项就是"后台改了但保存不生效"
                t.ProductType,
                t.CategoryId,
                t.BrandId,
                t.FreightTemplateId,
                t.Introduce,
                t.Unit,
                t.OriginalPrice,
                t.VideoUrl,
                t.DetailsHtml
            }, true);

            return result;
        }

        /// <summary>
        /// C 端按 SKU 批量取下单表单定义（结算页渲染输入框用）。
        ///
        /// 两次查询而非一次 join 投影：JSON 列（FormItems）在 SqlSugar 的 Select 投影里
        /// 按列名反序列化不稳，稳妥做法是取回实体再统一归一化。两次都是主键 IN，仍是 2 条 SQL。
        ///
        /// 只回在售商品的定义：结算页面对的必然是在售商品，下架的没必要把配置暴露出去。
        /// 字段定义经 <see cref="ProductFormValidator.ResolveFields"/> 归一化后下发，
        /// 前端拿到即可直接渲染，不必自己再兜一遍类型与排序。
        /// </summary>
        public List<FrontSkuFormDto> GetFrontFormFields(List<long> skuIds)
        {
            if (skuIds == null || skuIds.Count == 0)
            {
                return new List<FrontSkuFormDto>();
            }

            // 上限保护：参数被改成成百上千个 SKU 时截断（结算页用不到那么多），
            // 免得拼出超长 IN 把库拖住。截断而非报错：多出来的行前端本来也不会渲染。
            var ids = skuIds.Where(x => x > 0).Distinct().Take(MaxFormFieldSkuQuery).ToList();
            if (ids.Count == 0)
            {
                return new List<FrontSkuFormDto>();
            }

            var skuMap = Context.Queryable<Skus>()
                .Where(s => ids.Contains(s.SkuId) && s.IsDelete == 0)
                .Select(s => new { s.SkuId, s.ProductId, s.SpecCombination })
                .ToList();
            if (skuMap.Count == 0)
            {
                return new List<FrontSkuFormDto>();
            }

            var productIds = skuMap.Select(x => x.ProductId).Distinct().ToList();
            var products = Context.Queryable<Product>()
                .Where(p => productIds.Contains(p.ProductId)
                    && p.IsDelete == 0
                    && p.SaleStatus == SaleStatus.OnSale)
                .ToList();
            var productMap = products.ToDictionary(p => p.ProductId);

            return skuMap
                .Where(x => productMap.ContainsKey(x.ProductId))
                .Select(x =>
                {
                    var product = productMap[x.ProductId];
                    return new FrontSkuFormDto
                    {
                        SkuId = x.SkuId,
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        SkuSpec = x.SpecCombination,
                        ProductType = product.ProductType,
                        FormItems = ProductFormValidator.ResolveFields(product)
                    };
                })
                .ToList();
        }

        /// <summary>
        /// 下单表单设计器元数据：类型词表与各项限制全部取自服务端常量
        /// （<see cref="Enum.ProductFormType"/>、<see cref="ProductFormValidator"/>），不查库。
        ///
        /// 下发限制而不是让前端写死，是为了让"页面上能配什么"与服务端"实际允许什么"同源 ——
        /// 前端少一个限制只是体验差一点，服务端才是判定点。
        /// </summary>
        public ProductFormMetaDto GetFormMeta()
        {
            return new ProductFormMetaDto
            {
                Types = ProductFormType.BuiltIn.Select(type => new ProductFormTypeDto
                {
                    Value = type,
                    Label = ProductFormType.Label(type),
                    NeedOptions = ProductFormType.NeedOptions(type),
                    DefaultSensitive = ProductFormType.DefaultSensitive(type),
                    DefaultMaxLength = ProductFormValidator.DefaultMaxLengthOf(type)
                }).ToList(),
                // 预设字段：后台勾选即可生成字段定义，不必做拖拽式设计器。
                // 类型/长度等仍取自词表与校验器常量，避免"预设"另成一套口径。
                Presets = ProductFormPreset.Presets.Select(preset => new ProductFormPresetDto
                {
                    Key = preset.Key,
                    Label = preset.Label,
                    Type = preset.Type,
                    TypeLabel = ProductFormType.Label(preset.Type),
                    NeedOptions = ProductFormType.NeedOptions(preset.Type),
                    DefaultSensitive = ProductFormType.DefaultSensitive(preset.Type),
                    Required = preset.Required,
                    Placeholder = preset.Placeholder,
                    DefaultMaxLength = ProductFormValidator.DefaultMaxLengthOf(preset.Type)
                }).ToList(),
                MaxFieldCount = ProductFormValidator.MaxFieldCount,
                MaxFieldKeyLength = ProductFormValidator.MaxFieldKeyLength,
                MaxLabelLength = ProductFormValidator.MaxLabelLength,
                MaxOptionsLength = ProductFormValidator.MaxOptionsLength,
                MaxPatternLength = ProductFormValidator.MaxPatternLength,
                ValueHardLimit = ProductFormValidator.ValueHardLimit
            };
        }

        /// <summary>
        /// 导出商品管理
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ProductDto> ExportList(ShoppingProductQueryDto parm)
        {
            // 导出仅后台使用，无 C 端关键词检索
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Includes(x => x.Skus.Where(f => f.IsDelete == 0).ToList())
                .Includes(x => x.Category)
                .Includes(x => x.Brand)
                .Select((it) => new ProductDto
                {
                    Stock = it.Skus.Sum(s => s.Stock),
                    TotalSalesVolume = it.Skus.Sum(s => s.SalesVolume),
                    BrandName = it.Brand.Name,
                    CategoryName = it.Category.Name,
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 查询条件表达式（后台与 C 端共用）。C 端关键词条件不在这里 —— 它需要全文谓词，
        /// 统一由 <see cref="BuildProductSearchCondition"/> 生成参数化 raw SQL，在 QueryList 里拼。
        /// </summary>
        /// <param name="parm">查询参数</param>
        /// <returns></returns>
        private static Expressionable<Product> QueryExp(ShoppingProductQueryDto parm)
        {
            var predicate = Expressionable.Create<Product>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.ProductName), it => it.ProductName.Contains(parm.ProductName));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.ProductCode), it => it.ProductCode == parm.ProductCode);
            // 仅看有货：读维护好的总库存列（依赖 ProductTotalsRefresher 回写，需先执行回填 SQL）
            predicate = predicate.AndIF(parm.OnlyInStock, it => it.TotalStock > 0);
            predicate = predicate.AndIF(parm.CategoryId != null, it => it.CategoryId == parm.CategoryId);
            predicate = predicate.AndIF(parm.BrandId != null, it => it.BrandId == parm.BrandId);
            predicate = predicate.AndIF(parm.ProductId != null, it => it.ProductId == parm.ProductId);
            predicate = predicate.AndIF(parm.SaleStatus != null, it => it.SaleStatus == parm.SaleStatus);
            //predicate = predicate.AndIF(parm.BeginAddTime == null, it => it.AddTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            predicate = predicate.AndIF(parm.BeginAddTime != null, it => it.AddTime >= parm.BeginAddTime);
            predicate = predicate.AndIF(parm.EndAddTime != null, it => it.AddTime <= parm.EndAddTime);
            predicate = predicate.And(it => it.IsDelete == 0);
            return predicate;
        }

        /// <summary>
        /// 构造 C 端商品搜索条件（SQL 片段 + 参数），keyword 为空时返回 (null, null)。
        ///
        /// 只搜商品名（成本最低、中文分词效果最好）：
        /// - 已建全文索引 + SqlServer：<c>CONTAINS(ProductName, @kw)</c>；
        /// - 已建全文索引 + MySql：<c>MATCH(ProductName) AGAINST (@kw)</c>；
        /// - 其余（未建索引 / 其他库 / 探测失败）：<c>ProductName LIKE @kwLike</c>（前导通配，数据量大时慢）。
        ///
        /// 关键：FTS 就绪时**不能**再 OR 一个 LIKE 兜底 —— OR 分支不可索引，会让全文索引白建、仍走全表扫，
        /// 所以两条路径严格二选一（与文章检索 BuildArticleSearchCondition 同一口径）。
        /// </summary>
        private (string Sql, object Parameters) BuildProductSearchCondition(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return (null, null);

            if (IsProductNameFullTextReady() && IsFullTextKeywordSafe(keyword))
            {
                var dbType = Context.CurrentConnectionConfig.DbType;
                if (dbType == SqlSugar.DbType.SqlServer)
                {
                    // 对应 CREATE FULLTEXT INDEX ON mms_product([ProductName])，见 document/doc 的建索引脚本
                    return ("CONTAINS(ProductName, @kw)", new { kw = keyword });
                }
                if (dbType == SqlSugar.DbType.MySql)
                {
                    // 对应 ALTER TABLE mms_product ADD FULLTEXT INDEX ft_product_name (ProductName) WITH PARSER ngram
                    return ("MATCH(ProductName) AGAINST (@kw)", new { kw = keyword });
                }
            }

            // 回落 LIKE：先转义通配符，让用户输入的 % / _ 按字面匹配
            return ("ProductName LIKE @kwLike", new { kwLike = $"%{EscapeLikeKeyword(keyword)}%" });
        }

        /// <summary>
        /// 关键词是否可安全交给全文谓词：SQL Server 的 <c>CONTAINS</c> 会把 <c>" ( ) * ~</c> 当检索语法，
        /// 直接透传用户输入可能触发语法错误（接口 500）。含这些字符时改为回落 LIKE，
        /// 保证任意输入都不会报错；正常关键词（中文/字母/数字）不受影响，仍走全文索引。
        /// </summary>
        private static bool IsFullTextKeywordSafe(string keyword)
        {
            foreach (var ch in keyword)
            {
                if (ch is '"' or '(' or ')' or '*' or '~')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 商品名全文索引是否就绪：SqlServer 查 sys.fulltext_indexes，MySql 查 information_schema.STATISTICS。
        /// 结果按连接（租户库）缓存 10 分钟 —— 索引属部署动作，建好后最多 10 分钟自动生效，无需重启；
        /// 探测失败一律按"未就绪"处理，让搜索退化为 LIKE，不能因为索引缺失让搜索不可用。
        /// </summary>
        private bool IsProductNameFullTextReady()
        {
            string configId = Context.CurrentConnectionConfig.ConfigId?.ToString();
            string cacheKey = $"PRODUCT_NAME_FTS_READY_{configId}";
            if (CacheHelper.Exists(cacheKey))
            {
                return CacheHelper.GetCache(cacheKey)?.ToString() == "1";
            }

            var ready = false;
            try
            {
                var dbType = Context.CurrentConnectionConfig.DbType;
                if (dbType == SqlSugar.DbType.SqlServer)
                {
                    ready = Context.Ado.GetInt(
                        "SELECT COUNT(1) FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('mms_product')") > 0;
                }
                else if (dbType == SqlSugar.DbType.MySql)
                {
                    ready = Context.Ado.GetInt(
                        "SELECT COUNT(1) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'mms_product' AND INDEX_NAME = @indexName",
                        new { indexName = ProductNameFtsIndex }) > 0;
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"检测商品名全文索引失败，本次按 LIKE 检索：{ex.Message}");
            }

            CacheHelper.SetCache(cacheKey, ready ? "1" : "0", 10);
            return ready;
        }
    }
}