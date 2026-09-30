using Infrastructure.Extensions;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 
    /// </summary>
    [AppService(ServiceType = typeof(ICategoryService))]
    public class CategoryService : BaseService<Category>, ICategoryService
    {
        /// <summary>
        /// 查询目录列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<CategoryDto> GetList(ShoppingCategoryQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .WithCache(60 * 5)
                .ToPage<Category, CategoryDto>(parm);

            return response;
        }

        /// <summary>
        /// 查询目录树列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public List<CategoryDto> GetTreeList(ShoppingCategoryQueryDto parm)
        {
            var predicate = QueryExp(parm);
            var response = Queryable()
                .Where(predicate.ToExpression());

            if (parm.Sort.IsNotEmpty())
            {
                response = response.OrderByPropertyName(parm.Sort, parm.SortType.Contains("desc") ? OrderByType.Desc : OrderByType.Asc);
            }
            var treeList = response.ToTree(it => it.Children, it => it.ParentId, 0);

            return treeList.Adapt<List<CategoryDto>>();
        }

        /// <summary>
        /// C 端可见分类：ShowStatus=1（展示中）+ 未删除，按 OrderNum 升序。
        ///
        /// 走查询缓存（后端由 RedisServer:dbCache 决定 Redis 或内存）：数据量小、读极多写极少，
        /// 后台增删改 mms_category 时 IsAutoRemoveDataCache 会自动失效，无需手动清理。
        /// 原实现是 Controller 里 GetAll().Where(...)：把整表拉进内存再过滤，且完全没有缓存。
        /// </summary>
        public List<CategoryDto> GetFrontCategories()
        {
            return Queryable()
                .WithCache(60 * 5)
                .Where(c => c.ShowStatus == 1 && c.IsDelete == 0)
                .OrderBy(c => c.OrderNum)
                .ToList()
                .Adapt<List<CategoryDto>>();
        }

        /// <summary>
        /// 添加目录
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public int AddCategory(Category parm)
        {
            var response = Add(parm);
            return response;
        }

        /// <summary>
        /// 导出
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<CategoryDto> ExportList(ShoppingCategoryQueryDto parm)
        {
            var predicate = QueryExp(parm);

            var response = Queryable()
                .Where(predicate.ToExpression())
                .Select((it) => new CategoryDto()
                {
                }, true)
                .ToPage(parm);

            return response;
        }
        /// <summary>
        /// 查询导出表达式
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        private static Expressionable<Category> QueryExp(ShoppingCategoryQueryDto parm)
        {
            var predicate = Expressionable.Create<Category>();
            predicate.AndIF(parm.ShowStatus != null, m => m.ShowStatus == parm.ShowStatus);
            predicate.AndIF(parm.ParentId != null, m => m.ParentId == parm.ParentId);
            predicate.AndIF(parm.Name.IsNotEmpty(), m => m.Name.Contains(parm.Name));

            return predicate;
        }
    }
}
