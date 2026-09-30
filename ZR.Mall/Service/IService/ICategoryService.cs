using ZR.Mall.Model;
using ZR.Mall.Model.Dto;

namespace ZR.Mall.Service.IService
{
    public interface ICategoryService : IBaseService<Category>
    {
        PagedInfo<CategoryDto> GetList(ShoppingCategoryQueryDto parm);
        List<CategoryDto> GetTreeList(ShoppingCategoryQueryDto parm);

        /// <summary>
        /// C 端可见分类（展示中 + 未删除，按 OrderNum 升序），供首页/分类页筛选使用。
        /// 数据量小、读极多写极少，走查询缓存。
        /// </summary>
        List<CategoryDto> GetFrontCategories();

        int AddCategory(Category parm);
        PagedInfo<CategoryDto> ExportList(ShoppingCategoryQueryDto parm);
    }
}
