using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model
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
        /// 总记录数
        /// </summary>
        public int TotalNum { get; set; }
        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPage
        {
            get
            {
                if (TotalNum > 0)
                {
                    return TotalNum % this.PageSize == 0 ? TotalNum / this.PageSize : TotalNum / this.PageSize + 1;
                }
                else
                {
                    return 0;
                }
            }
            set { }
        }
        public List<T> Result { get; set; }
        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();
        public PagedInfo()
        {
        }
    }
}
