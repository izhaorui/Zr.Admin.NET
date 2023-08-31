using System.Collections.Generic;

namespace Infrastructure.Model
{
    /// <summary>
    /// 获取配置文件POCO实体类
    /// </summary>
    public class OptionsSetting
    {
        /// <summary>
        /// 是否单点登录
        /// </summary>
        public bool SingleLogin { get; set; }
        /// <summary>
        /// 是否演示模式
        /// </summary>
        public bool DemoMode { get; set; }
        /// <summary>
        /// 初始化db
        /// </summary>
        public bool InitDb { get; set; }
        public MailOptions MailOptions { get; set; }
        public Upload Upload { get; set; }
        public ALIYUN_OSS ALIYUN_OSS { get; set; }
        public JwtSettings JwtSettings { get; set; }
        public Gen Gen { get; set; }
        public List<DbConfigs> DbConfigs { get; set; }
        public DbConfigs CodeGenDbConfig { get; set; }
    }
    /// <summary>
    /// 发送邮件数据配置
    /// </summary>
    public class MailOptions
    {
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public string Password { get; set; }
        public string Smtp { get; set; }
        public int Port { get; set; }
        public bool UseSsl { get; set; }
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
        /// <summary>
        /// 刷新token时长
        /// </summary>
        public int RefreshTokenTime { get; set; }
        /// <summary>
        /// token类型
        /// </summary>
        public string TokenType { get; set; } = "Bearer";
    }

    public class Gen
    {
        public bool ShowApp { get; set; }
        public bool AutoPre { get; set; }
        public string VuePath { get; set; }
        public string Author { get; set; }
        public DbConfigs GenDbConfig { get; set; }
        public CsharpTypeArr CsharpTypeArr { get; set; }
    }

    public class DbConfigs
    {
        public string Conn { get; set; }
        public int DbType { get; set; }
        public string ConfigId { get; set; }
        public bool IsAutoCloseConnection { get; set; }
        public string DbName { get; set; }
    }

    public class CsharpTypeArr
    {
        public string[] String { get; set; }
        public string[] Int { get; set; }
        public string[] Long { get; set; }
        public string[] DateTime { get; set; }
        public string[] Float { get; set; }
        public string[] Decimal { get; set; }
        public string[] Bool { get; set; }
    }
}
