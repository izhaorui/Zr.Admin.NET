using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model
{
    public class PagerInfo
    {
        /// <summary>
        /// 当前页码
        /// </summary>
        public int PageNum { get; set; }
        private int pageSize;
        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalNum { get; set; }
        /// <summary>
        /// 总页码
        /// </summary>
        public int TotalPageNum { get; set; }
        public int PageSize { get => pageSize; set => pageSize = value; }

        public PagerInfo()
        {
            PageNum = 1;
            PageSize = 20;
        }

        public PagerInfo(int page = 1, int pageSize = 20)
        {
            PageNum = page;
            PageSize = pageSize;
        }
    }
}
