using ZR.Mall.Common;
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
        /// 查询商品管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ProductDto> GetList(ShoppingProductQueryDto parm)
        {
            return QueryList(parm, useCache: false);
        }

        /// <summary>
        /// C 端商品列表。只让「首页默认列表」进查询缓存：无任何筛选条件 + 第 1 页。
        /// 这类查询参数固定、重复率最高；带筛选/翻页的组合无限，缓存键会随之膨胀，故不缓存。
        ///
        /// 说明：SaleStatus 由 C 端 Controller 强制为「在售」，是常量，不参与「是否默认查询」的判定，
        /// 否则条件永远不成立、缓存形同虚设。
        /// </summary>
        public PagedInfo<ProductDto> GetFrontList(ShoppingProductQueryDto parm)
        {
            var isDefaultList = parm.PageNum <= 1
                && string.IsNullOrWhiteSpace(parm.ProductName)
                && string.IsNullOrWhiteSpace(parm.ProductCode)
                && parm.ProductId == null
                && parm.CategoryId == null
                && parm.BrandId == null
                && parm.BeginAddTime == null
                && parm.EndAddTime == null;

            return QueryList(parm, useCache: isDefaultList);
        }

        /// <summary>
        /// 商品列表查询主体：后台走 GetList（不缓存），C 端走 GetFrontList（仅默认列表缓存）。
        ///
        /// 缓存注意：本查询 Includes(Skus) 会 JOIN mms_skus，下单扣库存与 SKU 维护都会触发
        /// IsAutoRemoveDataCache 自动清缓存 —— 好处是库存/销量不会被缓存拖旧（成交后即刷新），
        /// 代价是命中率受写入频率影响。TTL 60 秒仅作兜底。
        /// </summary>
        private PagedInfo<ProductDto> QueryList(ShoppingProductQueryDto parm, bool useCache)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .WithCacheIF(useCache, 60)
                .Where(predicate.ToExpression())
                .Includes(x => x.Skus.Where(f => f.IsDelete == 0).ToList())
                .Includes(x => x.Category)
                .Includes(x => x.Brand)
                .ToPage<Product, ProductDto>(parm);
            foreach (var item in response.Result)
            {
                item.Stock = item.Skus.Sum(s => s.Stock);
                item.TotalSalesVolume = item.Skus.Sum(s => s.SalesVolume);
            }
            return response;
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

            var model = dto.Adapt<Product>();
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
                        t.ProductName,
                        t.ProductCode,
                        t.MainImage,
                        t.ImageUrls,
                        t.VideoUrl,
                        t.SaleStatus,
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
                t.ProductName,
                t.MainImage,
                t.ImageUrls,
                t.SaleStatus,
                t.CategoryId,
                t.BrandId,
                t.Introduce,
                t.Unit,
                t.OriginalPrice,
                t.VideoUrl,
                t.DetailsHtml
            }, true);

            return result;
        }

        /// <summary>
        /// 导出商品管理
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ProductDto> ExportList(ShoppingProductQueryDto parm)
        {
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
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<Product> QueryExp(ShoppingProductQueryDto parm)
        {
            var predicate = Expressionable.Create<Product>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.ProductName), it => it.ProductName.Contains(parm.ProductName));
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.ProductCode), it => it.ProductCode == parm.ProductCode);
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
    }
}