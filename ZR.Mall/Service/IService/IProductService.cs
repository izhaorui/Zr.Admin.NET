using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 商品管理service接口
    /// </summary>
    public interface IProductService : IBaseService<Product>
    {
        PagedInfo<ProductDto> GetList(ShoppingProductQueryDto parm);

        /// <summary>
        /// C 端商品列表：仅「首页默认列表」（无关键词/编码/分类/品牌等筛选 + 第 1 页）走查询缓存；
        /// 带筛选或翻页的查询不进缓存（组合无限，缓存键会膨胀）。其余行为与 <see cref="GetList"/> 完全一致。
        /// </summary>
        PagedInfo<ProductDto> GetFrontList(ShoppingProductQueryDto parm);

        ProductDto GetInfo(long ProductId);
        Product AddShoppingProduct(ProductDto parm);
        Product UpdateShoppingProduct(ProductDto parm);
        long UpdateInfo(ProductDto dto);
        PagedInfo<ProductDto> ExportList(ShoppingProductQueryDto parm);
    }
}
