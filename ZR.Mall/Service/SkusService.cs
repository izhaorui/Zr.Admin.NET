using ZR.Common;
using ZR.Mall.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 商品库存Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISkusService))]
    public class SkusService : BaseService<Skus>, ISkusService
    {
        /// <summary>
        /// 查询商品库存列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<SkusDto> GetList(ShoppingSkusQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .LeftJoin<Product>((s, p) => s.ProductId == p.ProductId)
                .WhereIF(!string.IsNullOrWhiteSpace(parm.ProductCode), (s, p) => p.ProductCode.Contains(parm.ProductCode))
                .Select((s, p) => new SkusDto
                {
                    ProductName = p.ProductName,
                    ProductCode = p.ProductCode
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 获取详情（已软删除的 SKU 不返回）
        /// </summary>
        /// <param name="SkuId"></param>
        /// <returns></returns>
        public Skus GetInfo(long SkuId)
        {
            var response = Queryable()
                .Where(x => x.SkuId == SkuId && x.IsDelete == 0)
                .First();

            return response;
        }

        /// <summary>
        /// 添加商品库存
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public Skus AddShoppingSkus(Skus model)
        {
            // 规格校验必须放在 UseTran 之前：UseTran 会把异常吞成 result.ErrorMessage，
            // 之后统一抛"添加失败"，用户看不到具体原因（同 ProductSpecValidator 的处理方式）。
            EnsureSpecCombinationValid(model.ProductId, model.SpecCombination, 0);
            model.Specs = BuildSpecGroups(model.SpecCombination);

            var result = UseTran(() =>
            {
                Insertable(model).ExecuteReturnEntity();
                RefreshProductSummary(model.ProductId);
            });
            if (!result.IsSuccess)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "添加失败", result.ErrorMessage);
            }
            return model;
        }

        /// <summary>
        /// 修改商品库存
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public long UpdateShoppingSkus(Skus model)
        {
            // 同上：校验在事务外；排除自身后再判断规格组合是否与同商品其他 SKU 重复
            EnsureSpecCombinationValid(model.ProductId, model.SpecCombination, model.SkuId);
            model.Specs = BuildSpecGroups(model.SpecCombination);

            long affected = 0;
            var result = UseTran(() =>
            {
                affected = Update(model, true);
                RefreshProductSummary(model.ProductId);
            });
            if (!result.IsSuccess)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "修改失败", result.ErrorMessage);
            }
            return affected;
        }

        /// <summary>
        /// 删除商品库存（按主键，支持逗号分隔的批量），并同步所属商品的 SKU 汇总列
        /// </summary>
        public long DeleteShoppingSkus(string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);
            if (idArr.Length == 0)
            {
                return 0;
            }

            long affected = 0;
            var result = UseTran(() =>
            {
                // 归属关系要先查：删除之后就定位不到这些 SKU 属于哪个商品了
                var productIds = Queryable()
                    .Where(f => idArr.Contains(f.SkuId))
                    .Select(f => f.ProductId)
                    .Distinct()
                    .ToList();

                // 软删除：与商品编辑页删 SKU 保持一致。
                // mms_skus 有 IsDelete 列，且历史订单项仍会引用这些 SkuId，物理删除会让引用悬空。
                affected = Deleteable()
                    .Where(f => idArr.Contains(f.SkuId) && f.IsDelete == 0)
                    .IsLogic()
                    .ExecuteCommand();

                RefreshProductSummary(productIds.ToArray());
            });
            if (!result.IsSuccess)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "删除失败", result.ErrorMessage);
            }
            return affected;
        }

        /// <summary>
        /// 回写所属商品的 SKU 汇总列（最低价 / 最高价 / 规格摘要）。
        /// 这几列是冗余汇总（见 Product 实体注释），商品保存流程与独立 SKU 管理流程共用本方法，
        /// 避免"两处各算一次"导致的长期不同步（从 SKU 页改价后商品列表价格区间不更新就是这么来的）。
        /// </summary>
        /// <param name="productIds">商品 Id，可传多个</param>
        public void RefreshProductSummary(params long[] productIds)
        {
            foreach (var productId in productIds.Distinct().Where(id => id > 0))
            {
                var prices = Queryable()
                    .Where(f => f.ProductId == productId && f.IsDelete == 0)
                    .Select(f => f.Price)
                    .ToList();

                if (prices.Count == 0)
                {
                    // 该商品已无可用 SKU：保持原值。置 0 会让列表/前台显示 ￥0，反而更误导；
                    // 此刻商品本就该下架，价格区间不再有展示意义。
                    continue;
                }

                var min = prices.Min();
                var max = prices.Max();

                // 规格摘要取自规格定义表（mms_product_spec），而不是从各 SKU 的组合聚合：
                // 定义里存在、但暂时没有任何 SKU 的规格值，同样应该出现在商品规格摘要里。
                var definitions = LoadSpecDefinitions(productId);
                if (definitions.Count > 0)
                {
                    var summary = ProductSpecFormatter.FormatSummary(definitions);

                    Context.Updateable<Product>()
                        .SetColumns(p => new Product { Price = min, MaxPrice = max, SpecSummary = summary })
                        .Where(p => p.ProductId == productId)
                        .ExecuteCommand();
                }
                else
                {
                    // 规格定义缺失（历史数据或无规格商品）：不覆盖原有 SpecSummary，避免把有效摘要清空
                    Context.Updateable<Product>()
                        .SetColumns(p => new Product { Price = min, MaxPrice = max })
                        .Where(p => p.ProductId == productId)
                        .ExecuteCommand();
                }
            }
        }

        /// <summary>
        /// 校验 SKU 的规格组合是否由该商品的规格定义（mms_product_spec）构成，并检查同商品下是否重复。
        /// mms_product_spec 是规格名的唯一来源：SKU 侧只允许在已定义的维度与取值范围内组合，
        /// 否则会出现定义之外的取值 —— 前台选不到对应 SKU，商品规格与订单快照口径全部错乱。
        /// 校验必须放在事务之外：UseTran 会把异常吞成笼统的失败信息。
        /// </summary>
        /// <param name="productId">SKU 所属商品</param>
        /// <param name="specCombination">规格组合快照，形如 "颜色:红;尺寸:XL"</param>
        /// <param name="excludeSkuId">重复检查时排除自身（修改场景传当前 SkuId，新增传 0）</param>
        private void EnsureSpecCombinationValid(long productId, string specCombination, long excludeSkuId)
        {
            var definitions = LoadSpecDefinitions(productId);
            var pairs = ProductSpecFormatter.ParseCombination(specCombination);

            if (definitions.Count == 0)
            {
                // 无规格商品：不允许带规格组合，否则规格定义永远追不上 SKU
                if (pairs.Count > 0)
                {
                    throw new CustomException($"该商品未定义规格，不能设置规格组合「{specCombination}」");
                }
                return;
            }

            if (pairs.Count == 0)
            {
                throw new CustomException("该商品已定义规格，请为 SKU 选择完整的规格组合");
            }

            // 每个规格维度都必须且只能出现一次，且取值必须在该维度的可选值内
            foreach (var definition in definitions)
            {
                var matched = pairs
                    .Where(p => string.Equals(p.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matched.Count == 0)
                {
                    throw new CustomException($"规格组合缺少规格「{definition.Name}」");
                }
                if (matched.Count > 1)
                {
                    throw new CustomException($"规格组合中「{definition.Name}」重复出现");
                }

                var value = matched[0].Value;
                var allowed = definition.SpecValues ?? new List<string>();
                if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    throw new CustomException($"规格「{definition.Name}」没有取值「{value}」，请先在商品编辑页补充该规格值");
                }
            }

            // 反向检查：组合里出现了定义之外的规格维度
            var unknown = pairs.Find(p => !definitions.Any(d =>
                string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase)));
            if (unknown != null)
            {
                throw new CustomException($"规格「{unknown.Name}」不在该商品的规格定义中");
            }

            // 同一商品下不允许重复的规格组合
            var duplicated = Queryable().Any(f => f.ProductId == productId
                && f.SkuId != excludeSkuId
                && f.IsDelete == 0
                && f.SpecCombination == specCombination);
            if (duplicated)
            {
                throw new CustomException($"规格组合「{specCombination}」已存在，不能重复添加");
            }
        }

        /// <summary>
        /// 读取商品的规格定义（mms_product_spec）
        /// </summary>
        private List<ProductSpec> LoadSpecDefinitions(long productId)
        {
            return Context.Queryable<ProductSpec>()
                .Where(f => f.ProductId == productId)
                .ToList();
        }

        /// <summary>
        /// 由规格组合快照派生 Skus.Specs（JSON 列）的值。
        /// 该列原先只有商品保存流程会写，SKU 编辑路径因 Update 忽略 null 列而从不更新；
        /// 这里统一由 SpecCombination 派生，保证两者永远一致。无规格组合时返回 null（保持该列不更新）。
        /// </summary>
        private static List<ProductSpecGroup> BuildSpecGroups(string specCombination)
        {
            var groups = ProductSpecFormatter.ParseCombination(specCombination);
            return groups.Count > 0 ? groups : null;
        }

        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<Skus> QueryExp(ShoppingSkusQueryDto parm)
        {
            var predicate = Expressionable.Create<Skus>();
            predicate = predicate.AndIF(parm.ProductId.HasValue && parm.ProductId.Value > 0, x => x.ProductId == parm.ProductId.Value);
            // SKU 是软删除（见 DeleteShoppingSkus 的 IsLogic），列表必须排除已删除项，
            // 否则后台会列出已删除的 SKU —— 与商品列表（ProductService.QueryExp 有 IsDelete 条件）口径不一致。
            predicate = predicate.And(x => x.IsDelete == 0);

            return predicate;
        }

        public List<Skus> GetSkus(long productId)
        {
            return Queryable()
                .Where(f => f.ProductId == productId && f.IsDelete == 0)
                .ToList();
        }
    }
}