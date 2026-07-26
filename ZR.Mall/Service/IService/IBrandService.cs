using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 品牌表service接口
    /// </summary>
    public interface IBrandService : IBaseService<Brand>
    {
        PagedInfo<BrandDto> GetList(ShopBrandQueryDto parm);

        Brand GetInfo(long Id);

        Brand AddShopBrand(Brand parm);
        int UpdateShopBrand(Brand parm);

        /// <summary>
        /// 安全物理删除品牌：若品牌已被商品引用则拒绝，否则执行物理删除
        /// </summary>
        int DeleteBrand(long[] ids);

        PagedInfo<BrandDto> ExportList(ShopBrandQueryDto parm);
    }
}
