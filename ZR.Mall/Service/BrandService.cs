using ZR.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 品牌表Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IBrandService))]
    public class BrandService : BaseService<Brand>, IBrandService
    {
        /// <summary>
        /// 查询品牌表列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<BrandDto> GetList(ShopBrandQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToPage<Brand, BrandDto>(parm);

            return response;
        }


        /// <summary>
        /// 获取详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        public Brand GetInfo(long Id)
        {
            var response = Queryable()
                .Where(x => x.Id == Id)
                .First();

            return response;
        }

        /// <summary>
        /// 添加品牌表
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public Brand AddShopBrand(Brand model)
        {
            return Insertable(model).ExecuteReturnEntity();
        }

        /// <summary>
        /// 修改品牌表
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateShopBrand(Brand model)
        {
            return Update(model, true, "修改品牌表");
        }

        /// <summary>
        /// 导出品牌表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<BrandDto> ExportList(ShopBrandQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new BrandDto()
                {
                }, true)
                .ToPage(parm);

            return response;
        }

        /// <summary>
        /// 安全物理删除品牌
        /// </summary>
        /// <remarks>
        /// 删除前校验品牌是否被商品(Product.BrandId)引用，若有引用则拒绝删除以避免
        /// 触发外键异常或留下孤儿商品数据；无引用时执行真正的物理删除。
        /// </remarks>
        public int DeleteBrand(long[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;

            // 1. 查找被商品引用的品牌
            var usedBrandIds = Context.Queryable<Product>()
                .Where(p => p.BrandId != null && ids.Contains(p.BrandId.Value))
                .Select(p => p.BrandId.Value)
                .Distinct()
                .ToList();

            if (usedBrandIds.Count > 0)
            {
                var names = Context.Queryable<Brand>()
                    .Where(b => usedBrandIds.Contains(b.Id))
                    .Select(b => b.Name)
                    .ToList();
                throw new CustomException($"品牌【{string.Join("、", names)}】已被商品使用，请先解除商品关联后再删除");
            }

            // 2. 确认无引用后执行物理删除
            return Context.Deleteable<Brand>(ids).EnableDiffLogEventIF(true, "删除品牌表").ExecuteCommand();
        }

        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<Brand> QueryExp(ShopBrandQueryDto parm)
        {
            var predicate = Expressionable.Create<Brand>();

            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Name), it => it.Name == parm.Name);
            return predicate;
        }
    }
}