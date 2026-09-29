using ZR.Mall.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 商品规格Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IProductSpecService))]
    public class ProductSpecService : BaseService<ProductSpec>, IProductSpecService
    {
        /// <summary>
        /// 查询商品规格列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<ProductSpecDto> GetList(ShoppingProductSpecQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .ToPage<ProductSpec, ProductSpecDto>(parm);

            return response;
        }

        /// <summary>
        /// 添加商品规格
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ProductSpec AddShoppingProductspec(ProductSpec model)
        {
            return Insertable(model).ExecuteReturnEntity();
        }

        /// <summary>
        /// 修改商品规格
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="newSpecs"></param>
        /// <returns></returns>
        public long UpdateProductSpec(long productId, List<ProductSpec> newSpecs)
        {
            // 底层写入兜底校验：商品编辑入口已校验过，这里防的是将来新增调用方绕过校验直接写规格表
            // （mms_product_spec 是规格名的唯一来源，一旦写脏，SKU/订单/前台全部受影响）。
            ProductSpecValidator.Validate(newSpecs);

            var result = 0;
            // 获取数据库中旧数据
            var oldSpecs = GetList(f => f.ProductId == productId);
            // 提取 ID 列表
            var newIds = newSpecs.Where(x => x.Id > 0).Select(x => x.Id).ToList();
            var oldIds = oldSpecs.Select(x => x.Id).ToList();
            // ➤ 更新：有 ID 且在数据库中
            var updateSpecs = newSpecs.Where(x => x.Id > 0 && oldIds.Contains(x.Id)).ToList();
            foreach (var spec in updateSpecs)
            {
                result = Update(spec, true, "数据修改"); // 可带日志记录
            }
            // ➤ 插入：没有 ID 的新增项
            var insertSpecs = newSpecs.Where(x => x.Id <= 0).ToList();
            if (insertSpecs.Count != 0)
                InsertRange(insertSpecs);

            // ➤ 删除：数据库有但前端没传的
            var deleteIds = oldSpecs
                .Where(x => !newIds.Contains(x.Id)) // 不在新 ID 列表中
                .Select(x => x.Id)
                .ToList();

            if (deleteIds.Count != 0)
                Delete(deleteIds);
            return result;
        }

        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<ProductSpec> QueryExp(ShoppingProductSpecQueryDto parm)
        {
            var predicate = Expressionable.Create<ProductSpec>();

            return predicate;
        }

        public long DeleteSpecByProductId(long productId)
        {
            return Deleteable()
                .Where(f => f.ProductId == productId)
                .EnableDiffLogEvent("删除商品规格")
                .ExecuteCommand();
        }
    }
}