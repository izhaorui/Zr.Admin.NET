namespace ZR.Model.Dto
{
    public class UploadDto
    {
        /// <summary>
        /// 自定文件名
        /// </summary>
        public string FileName { get; set; }
        /// <summary>
        /// 存储目录
        /// </summary>
        public string FileDir { get; set; }
        /// <summary>
        /// 文件名生成类型 1 原文件名 2 自定义 3 自动生成
        /// </summary>
        public int FileNameType { get; set; }
    }
}
