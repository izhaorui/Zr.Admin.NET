namespace ZR.Model.System
{
    [Tenant("0")]
    [SugarTable("sys_file", "文件存储表")]
    public class SysFile
    {
        /// <summary>
        /// 自增id
        /// </summary>
        [JsonConverter(typeof(ValueToStringConverter))]
        [SugarColumn(IsPrimaryKey = true)]
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

        public SysFile() { }
        public SysFile(string originFileName, string fileName, string ext, string fileSize, string storePath, string create_by)
        {
            StorePath = storePath;
            RealName = originFileName;
            FileName = fileName;
            FileExt = ext;
            FileSize = fileSize;
            Create_by = create_by;
            Create_time = DateTime.Now;
        }
    }
}