using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data;

namespace ZR.Model.Vo
{
    public class VMPageResult<T> where T : new()
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalNum { get; set; }
        //public int TotalPages { get; set; }
        public IList<T> Result { get; set; }
        public long EndPage { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DataTable Data { get; set; }

        public VMPageResult()
        { 
        }

        /// <summary>
        /// 集合使用
        /// </summary>
        /// <param name="result"></param>
        /// <param name="pagerInfo"></param>
        public VMPageResult(IList<T> result, PagerInfo pagerInfo)
        {
            this.Result = result;
            this.TotalNum = pagerInfo.TotalNum;
            this.PageSize = pagerInfo.PageSize;
            this.Page = pagerInfo.PageNum;
            //计算
            this.EndPage = pagerInfo.TotalNum % PageSize == 0 ? pagerInfo.TotalNum / PageSize : pagerInfo.TotalNum / PageSize + 1;
        }

        /// <summary>
        /// datable使用
        /// </summary>
        /// <param name="result"></param>
        /// <param name="pagerInfo"></param>
        public VMPageResult(DataTable result, PagerInfo pagerInfo)
        {
            this.Data = result;
            this.TotalNum = pagerInfo.TotalNum;
            this.PageSize = pagerInfo.PageSize;
            this.Page = pagerInfo.PageNum;
            //计算
            this.EndPage = pagerInfo.TotalNum % PageSize == 0 ? pagerInfo.TotalNum / PageSize : pagerInfo.TotalNum / PageSize + 1;
        }
    }

}
