using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 商品库存service接口
    /// </summary>
    public interface ISkusService : IBaseService<Skus>
    {
        PagedInfo<SkusDto> GetList(ShoppingSkusQueryDto parm);
        Skus GetInfo(long SkuId);
        List<Skus> GetSkus(long ProductId);
        Skus AddShoppingSkus(Skus parm);
        long UpdateShoppingSkus(Skus parm);

        /// <summary>
        /// 删除商品库存（支持逗号分隔的批量），并同步所属商品的 SKU 汇总列
        /// </summary>
        long DeleteShoppingSkus(string ids);

        /// <summary>
        /// 回写商品的 SKU 汇总列（最低价 / 最高价 / 规格摘要）。
        /// 商品保存流程与独立 SKU 管理流程共用此方法，保证汇总列只有一处计算逻辑。
        /// </summary>
        /// <param name="productIds">商品 Id，可传多个</param>
        void RefreshProductSummary(params long[] productIds);
    }
}
