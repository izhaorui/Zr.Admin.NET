using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Model
{
    /// <summary>
    /// 分页参数
    /// </summary>
    public class PagedInfo<T>
    {
        /// <summary>
        /// 每页行数
        /// </summary>
        public int PageSize { get; set; } = 10;
        /// <summary>
        /// 当前页
        /// </summary>
        public int PageIndex { get; set; } = 1;
        /// <summary>
        /// 排序列
        /// </summary>
        //public string Sort { get; set; }
        /// <summary>
        /// 排序类型
        /// </summary>
        //public string SortType { get; set; }
        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// 总页数
        /// </summary>
        //public int TotalPage
        //{
        //    get
        //    {
        //        if (TotalCount > 0)
        //        {
        //            return TotalCount % this.PageSize == 0 ? TotalCount / this.PageSize : TotalCount / this.PageSize + 1;
        //        }
        //        else
        //        {
        //            return 0;
        //        }
        //    }
        //    set {  }
        //}
        public int TotalPage { get; set; }
        public List<T> Result { get; set; }

        public PagedInfo()
        { 
        }


        public PagedInfo(List<T> source, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = source.Count;
            TotalPage = (int)Math.Ceiling(TotalCount / (double)PageSize);//计算总页数
        }

    }
}
