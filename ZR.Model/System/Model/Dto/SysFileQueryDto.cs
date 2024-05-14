using System;

namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 文件存储输入对象
    /// </summary>
    public class SysFileDto
    {
        public long Id { get; set; }
        /// <summary>
        /// 文件原名
        /// </summary>
        public string RealName { get; set; }
        /// <summary>
        /// 文件类型
        /// </summary>
        public string FileType { get; set; }
        /// <summary>
        /// 存储文件名
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// 文件存储地址 eg：/uploads/20220202
        /// </summary>
        public string FileUrl { get; set; }
        /// <summary>
        /// 仓库位置 eg：/uploads
        /// </summary>
        public string StorePath { get; set; }
        /// <summary>
        /// 文件大小
        /// </summary>
        public string FileSize { get; set; }
        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string FileExt { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        public string Create_by { get; set; }
        /// <summary>
        /// 上传时间
        /// </summary>
        public DateTime? Create_time { get; set; }
        /// <summary>
        /// 存储类型
        /// </summary>
        public int? StoreType { get; set; }
        /// <summary>
        /// 访问路径
        /// </summary>
        public string AccessUrl { get; set; }

        public SysFileDto() { }
        public SysFileDto(string originFileName, string fileName, string ext, string fileSize, string storePath, string accessUrl, string create_by)
        {
            StorePath = storePath;
            RealName = originFileName;
            FileName = fileName;
            FileExt = ext;
            FileSize = fileSize;
            AccessUrl = accessUrl;
            Create_by = create_by;
            Create_time = DateTime.Now;
        }
    }
    public class SysFileQueryDto : PagerInfo
    {
        public DateTime? BeginCreate_time { get; set; }
        public DateTime? EndCreate_time { get; set; }
        public int? StoreType { get; set; }
        public long? FileId { get; set; }
    }
}
