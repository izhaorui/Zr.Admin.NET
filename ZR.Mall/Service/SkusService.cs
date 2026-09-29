using ZR.Common;
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
        /// 获取详情
        /// </summary>
        /// <param name="SkuId"></param>
        /// <returns></returns>
        public Skus GetInfo(long SkuId)
        {
            var response = Queryable()
                .Where(x => x.SkuId == SkuId)
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

                affected = Delete(idArr);
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
        /// 这几列是冗余汇总（见 Product 实体注释），原先只在商品保存流程由 ProductService.CalcProduct 计算；
        /// 独立的 SKU 管理入口（本 Service 的增改删）必须同样回写，
        /// 否则从 SKU 页面改价后，商品列表的价格区间与规格会一直停留在旧值。
        /// </summary>
        private void RefreshProductSummary(params long[] productIds)
        {
            foreach (var productId in productIds.Distinct().Where(id => id > 0))
            {
                var skus = Queryable()
                    .Where(f => f.ProductId == productId && f.IsDelete == 0)
                    .Select(f => new { f.Price, f.SpecCombination })
                    .ToList();

                if (skus.Count == 0)
                {
                    // 该商品已无可用 SKU：保持原值。置 0 会让列表/前台显示 ￥0，反而更误导；
                    // 此刻商品本就该下架，价格区间不再有展示意义。
                    continue;
                }

                var min = skus.Min(s => s.Price);
                var max = skus.Max(s => s.Price);
                var summary = BuildSpecSummary(skus.Select(s => s.SpecCombination));

                Context.Updateable<Product>()
                    .SetColumns(p => new Product { Price = min, MaxPrice = max, SpecSummary = summary })
                    .Where(p => p.ProductId == productId)
                    .ExecuteCommand();
            }
        }

        /// <summary>
        /// 由各 SKU 的规格组合快照重建商品规格摘要，格式与 ProductService.CalcProduct 保持一致：
        /// "颜色: 红/蓝, 尺码: S/M/L"。同名规格的取值按 SKU 出现顺序去重合并。
        /// 数据源取 SpecCombination 而非 Specs：SKU 编辑页只提交前者（Update 忽略 null 列），
        /// Specs 这条路径上不会更新，拿它聚合会得到过期结果。
        /// </summary>
        private static string BuildSpecSummary(IEnumerable<string> combinations)
        {
            // 规格名 -> 取值（按首次出现顺序去重）；同名规格忽略大小写（与 ProductSpecValidator 的判定口径一致）
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var combination in combinations)
            {
                if (string.IsNullOrWhiteSpace(combination))
                {
                    continue;
                }
                // 形如 "颜色:红;尺寸:XL"
                foreach (var pair in combination.Split(';', StringSplitOptions.RemoveEmptyEntries))
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

                    if (!groups.TryGetValue(name, out var values))
                    {
                        values = new List<string>();
                        groups[name] = values;
                    }
                    if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
                    {
                        values.Add(value);
                    }
                }
            }

            return string.Join(", ", groups.Select(g => $"{g.Key}: {string.Join("/", g.Value)}"));
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