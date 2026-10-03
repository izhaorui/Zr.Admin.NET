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

        /// <summary>
        /// 下单表单设计器元数据（内置字段类型 + 各项限制）。
        /// 纯常量装配、不查库：前端配置表单时据此渲染类型下拉与字数提示，
        /// 不必自己再维护一份类型映射表。
        /// </summary>
        ProductFormMetaDto GetFormMeta();

        /// <summary>
        /// C 端按 SKU 批量取下单表单定义（结算页渲染输入框用）。
        /// 已下架 / 不存在的 SKU 直接不出现在结果里（前端按缺项处理即可，不必报错）。
        /// </summary>
        List<FrontSkuFormDto> GetFrontFormFields(List<long> skuIds);
    }
}
