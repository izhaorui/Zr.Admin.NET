using Infrastructure.Attribute;
using SqlSugar;
using SqlSugar.IOC;
using System.Collections.Generic;
using System.Linq;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 文章目录
    /// </summary>
    [AppService(ServiceType = typeof(IArticleCategoryService), ServiceLifetime = LifeTime.Transient)]
    public class ArticleCategoryService : BaseService<ArticleCategory>, IArticleCategoryService
    {
        /// <summary>
        /// 构建前端所需要树结构
        /// </summary>
        /// <param name="categories">目录列表</param>
        /// <returns></returns>
        public List<ArticleCategory> BuildCategoryTree(List<ArticleCategory> categories)
        {
            List<ArticleCategory> returnList = new List<ArticleCategory>();
            List<int> tempList = categories.Select(f => f.Category_Id).ToList();
            foreach (var dept in categories)
            {
                // 如果是顶级节点, 遍历该父节点的所有子节点
                if (!tempList.Contains(dept.ParentId))
                {
                    RecursionFn(categories, dept);
                    returnList.Add(dept);
                }
            }

            if (!returnList.Any())
            {
                returnList = categories;
            }
            return returnList;
        }

        /// <summary>
        /// 递归列表
        /// </summary>
        /// <param name="list"></param>
        /// <param name="t"></param>
        private void RecursionFn(List<ArticleCategory> list, ArticleCategory t)
        {
            //得到子节点列表
            List<ArticleCategory> childList = GetChildList(list, t);
            t.Children = childList;
            foreach (var item in childList)
            {
                if (GetChildList(list, item).Count() > 0)
                {
                    RecursionFn(list, item);
                }
            }
        }

        /// <summary>
        /// 递归获取子菜单
        /// </summary>
        /// <param name="list">所有菜单</param>
        /// <param name="dept"></param>
        /// <returns></returns>
        private List<ArticleCategory> GetChildList(List<ArticleCategory> list, ArticleCategory t)
        {
            return list.Where(p => p.ParentId == t.Category_Id).ToList();
        }
    }
}
