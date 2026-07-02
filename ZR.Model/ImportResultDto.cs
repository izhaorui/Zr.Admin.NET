using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.Model
{
    public class ImportResultDetailDto
    {
        /// <summary>
        /// 错误/忽略信息
        /// </summary>
        public string StorageMessage { get; set; }

        /// <summary>
        /// 对应记录数据
        /// </summary>
        public object Record { get; set; }
    }

    public class ImportResultDto
    {
        /// <summary>
        /// 总数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 成功数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败数
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// 忽略数
        /// </summary>
        public int IgnoreCount { get; set; }

        /// <summary>
        /// 插入数
        /// </summary>
        public int InsertCount { get; set; }

        /// <summary>
        /// 更新数
        /// </summary>
        public int UpdateCount { get; set; }

        /// <summary>
        /// 结果摘要
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// 错误明细前N条
        /// </summary>
        public List<ImportResultDetailDto> ErrorDetails { get; set; } = new();

        /// <summary>
        /// 忽略明细前N条
        /// </summary>
        public List<ImportResultDetailDto> IgnoreDetails { get; set; } = new();
    }
}
