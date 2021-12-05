using System.Collections.Generic;

namespace ZR.Model
{
    /// <summary>
    /// 分页扩展
    /// </summary>
    public static class PageExtension
    {
        /// <summary>
        /// 读取列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        public static PagedInfo<T> ToPage<T>(this List<T> source, PagerInfo parm)
        {
            var page = new PagedInfo<T>
            {
                TotalPage = parm.TotalPage,
                TotalNum = parm.TotalNum,
                PageSize = parm.PageSize,
                PageIndex = parm.PageNum,
                Result = source
            };
            return page;
        }
    }
}
