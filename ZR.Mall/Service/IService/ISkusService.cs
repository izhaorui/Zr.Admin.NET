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
    }
}
