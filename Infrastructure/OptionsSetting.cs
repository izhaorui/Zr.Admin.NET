
namespace Infrastructure
{
    /// <summary>
    /// 获取配置文件POCO实体类
    /// </summary>
    public class OptionsSetting
    {
        /// <summary>
        /// 是否演示模式
        /// </summary>
        public bool DemoMode { get; set; }
        public MailOptions MailOptions { get; set; }
        public Upload Upload { get; set; }
        public ALIYUN_OSS ALIYUN_OSS { get; set; }
        public JwtSettings JwtSettings { get; set; }
    }
    /// <summary>
    /// 发送邮件数据配置
    /// </summary>
    public class MailOptions
    {
        public string From { get; set; }
        public string Password { get; set; }
        public string Smtp { get; set; }
        public int Port { get; set; }
        public string Signature { get; set; }
    }
    /// <summary>
    /// 上传
    /// </summary>
    public class Upload
    {
        public string UploadUrl { get; set; }
        public string LocalSavePath { get; set; }
        public int MaxSize { get; set; }
        public string[] NotAllowedExt { get; set; } = new string[0];
    }
    /// <summary>
    /// 阿里云存储
    /// </summary>
    public class ALIYUN_OSS
    {
        public string REGIONID { get; set; }
        public string KEY { get; set; }
        public string SECRET { get; set; }
        public string BucketName { get; set; }
        public string DomainUrl { get; set; }
        public int MaxSize { get; set; } = 100;
    }

    /// <summary>
    /// Jwt
    /// </summary>
    public class JwtSettings
    {
        /// <summary>
        /// token是谁颁发的
        /// </summary>
        public string Issuer { get; set; }
        /// <summary>
        /// token可以给那些客户端使用
        /// </summary>
        public string Audience { get; set; }
        /// <summary>
        /// 加密的key（SecretKey必须大于16个,是大于，不是大于等于）
        /// </summary>
        public string SecretKey { get; set; }
        /// <summary>
        /// token时间（分）
        /// </summary>
        public int Expire { get; set; } = 1440;
    }
}
