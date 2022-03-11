using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 文件存储输入对象
    /// </summary>
    public class SysFileDto
    {
        public long Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string StorePath { get; set; }
        public string FileSize { get; set; }
        public string FileExt { get; set; }
        public string Create_by { get; set; }
        public DateTime? Create_time { get; set; }
        public int? StoreType { get; set; }
        public string AccessUrl { get; set; }
    }
    public class SysFileQueryDto : PagerInfo
    {
        public DateTime? BeginCreate_time { get; set; }
        public DateTime? EndCreate_time { get; set; }
        public int? StoreType { get; set; }
        public long? FileId { get; set; }
    }
}
