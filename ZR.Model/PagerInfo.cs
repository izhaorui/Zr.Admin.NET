namespace ZR.Model
{
    public class PagerInfo
    {
        /// <summary>
        /// 当前页码
        /// </summary>
        public int PageNum { get; set; }
        /// <summary>
        /// 每页显示多少条
        /// </summary>
        public int PageSize { get; set; }
        /// <summary>
        /// 总记录数
        /// </summary>
        public int TotalNum { get; set; }
        /// <summary>
        /// 排序字段
        /// </summary>
        public string Sort { get; set; } = string.Empty;
        /// <summary>
        /// 排序类型,前端传入的是"ascending"，"descending"
        /// </summary>
        public string SortType { get; set; } = string.Empty;
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
