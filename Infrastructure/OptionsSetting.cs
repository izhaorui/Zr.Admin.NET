
namespace Infrastructure
{
    /// <summary>
    /// 获取配置文件POCO实体类
    /// </summary>
    public class OptionsSetting
    {
        public static string ConnAdmin = "conn_zrAdmin";
        public static string ConnDbType = "conn_zrAdmin_Type";
        public static string DbKey = "DbKey";

        public string Redis { get; set; }
        public string Database { get; set; }
        /// <summary>
        /// 是否演示模式
        /// </summary>
        public bool DemoMode { get; set; }
        public MailOptions MailOptions { get; set; }
        public Upload Upload { get; set; }
        public ALYUN_OCS ALYUN_OCS { get; set; }
    }
    /// <summary>
    /// 发送邮件数据配置
    /// </summary>
    public class MailOptions
    {
        public string From { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
    }
    /// <summary>
    /// 上传
    /// </summary>
    public class Upload
    {
        public string UploadDirectory { get; set; }
        public string UploadUrl { get; set; }
    }
    /// <summary>
    /// 阿里云存储
    /// </summary>
    public class ALYUN_OCS
    {
        public string REGIONID { get; set; }
        public string KEY { get; set; }
        public string SECRET { get; set; }
    }
}
