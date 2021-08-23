
namespace ZRModel
{
    /// <summary>
    /// 获取配置文件POCO实体类
    /// </summary>
    public class OptionsSetting
    {
        /// <summary>
        /// 1、喵播 2、fireStar
        /// </summary>
        public int Platform { get; set; }
        public string AppName { get; set; }
        public string Redis { get; set; }
        public string Conn_Live { get; set; }
        public string Conn_Admin { get; set; }
        /// <summary>
        /// mongodb连接字符串
        /// </summary>
        public string Mongo_Conn { get; set; }
        public string Database { get; set; }
        
        public LoggingOptions Logging { get; set; }
        public MailOptions MailOptions { get; set; }
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
    /// 日志
    /// </summary>
    public class LoggingOptions
    {
        public LogLevelOptions LogLevel { get; set; }
    }
    public class LogLevelOptions
    {
        public string Default { get; set; }
        public string Ssytem { get; set; }
        public string Microsoft { get; set; }
    }

}
