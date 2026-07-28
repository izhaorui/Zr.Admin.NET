using System.Collections.Generic;

namespace Infrastructure.Model
{
    /// <summary>
    /// 获取配置文件POCO实体类
    /// </summary>
    public class OptionsSetting
    {
        /// <summary>
        /// 是否单设备登录
        /// </summary>
        public bool SingleLogin { get; set; }
        /// <summary>
        /// 是否演示模式
        /// </summary>
        public bool DemoMode { get; set; }
        /// <summary>
        /// 初始化db（建表）。设为 true 启动时会自动建表，并自动导入种子数据
        /// </summary>
        public bool InitDb { get; set; }
        /// <summary>
        /// 是否在 InitDb 建表后自动导入种子数据（data.xlsx）。默认 true，无需手动调用 InitSeedData 接口
        /// </summary>
        public bool InitSeed { get; set; } = true;
        /// <summary>
        /// 是否单独初始化商城模块（开发模式下建商城业务表 + 商城菜单种子）。独立于 InitDb，
        /// 可在 InitDb=false 时单独设为 true 来只初始化商城（建表 + 商城菜单），不影响其他模块初始化。
        /// </summary>
        public bool InitMall { get; set; }
        /// <summary>数据库迁移配置（自动发现实体、差异检测、迁移历史）</summary>
        public DbMigrationOptions DbMigration { get; set; } = new();
        public string[] InitTables { get; set; }
        /// <summary>
        /// 邮箱配置
        /// </summary>
        public List<MailOptions> MailOptions { get; set; }
        /// <summary>
        /// 短信配置
        /// </summary>
        public SmsOptions SmsOptions { get; set; }
        /// <summary>
        /// 上传配置
        /// </summary>
        public Upload Upload { get; set; }
        /// <summary>
        /// 阿里云oss
        /// </summary>
        public ALIYUN_OSS ALIYUN_OSS { get; set; }
        public JwtSettings JwtSettings { get; set; }
        /// <summary>
        /// 代码生成配置
        /// </summary>
        public CodeGen CodeGen { get; set; }
        /// <summary>
        /// 数据库集合
        /// </summary>
        public List<DbConfigs> DbConfigs { get; set; }
        /// <summary>
        /// 代码生成数据库配置
        /// </summary>
        public DbConfigs CodeGenDbConfig { get; set; }
        /// <summary>
        /// Reids配置
        /// </summary>
        public RedisServerConfig RedisServer { get; set; }
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
    /// 短信服务配置
    /// </summary>
    public class SmsOptions
    {
        /// <summary>是否启用真实发送。false 时为模拟发送（仅记录日志），不影响业务流程</summary>
        public bool Enabled { get; set; }
        /// <summary>服务商：None/Aliyun/TencentCloud（对接时在 DefaultSmsSender 对应分支实现）</summary>
        public string Provider { get; set; } = "None";
        /// <summary>密钥ID（阿里云 AccessKeyId / 腾讯云 SecretId）</summary>
        public string AccessKeyId { get; set; }
        /// <summary>密钥Secret（阿里云 AccessKeySecret / 腾讯云 SecretKey）</summary>
        public string AccessKeySecret { get; set; }
        /// <summary>短信签名（如：ZRAdmin）</summary>
        public string SignName { get; set; }
        /// <summary>服务端点/地域（如阿里云 dysmsapi.aliyuncs.com、腾讯云 ap-guangzhou）</summary>
        public string Endpoint { get; set; }
        /// <summary>应用ID（腾讯云 SdkAppId 等，部分服务商需要）</summary>
        public string SdkAppId { get; set; }
        /// <summary>业务场景到模板编号映射（如 login→SMS_10001，方便按场景取模板）</summary>
        public Dictionary<string, string> Templates { get; set; }
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

    public class CodeGen
    {
        /// <summary>
        /// 是否显示移动端代码生成
        /// </summary>
        public bool ShowApp { get; set; }
        /// <summary>
        /// 是否自动去除前缀
        /// </summary>
        public bool AutoPre { get; set; }
        /// <summary>
        /// vue前端生成路径
        /// </summary>
        public string VuePath { get; set; }
        /// <summary>
        /// 作者
        /// </summary>
        public string Author { get; set; }
        public string TablePrefix { get; set; }
        /// <summary>
        /// 模块名，默认值：business
        /// </summary>
        public string ModuleName { get; set; }
        public int FrontTpl { get; set; }
        /// <summary>
        /// unipap vue版本号可选值2/3
        /// </summary>
        public int UniappVersion { get; set; } = 2;
        /// <summary>
        /// unipap前端存储路径
        /// </summary>
        public string UniappPath { get; set; }
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

    public class RedisServerConfig
    {
        public int Open { get; set; }
        public bool DbCache { get; set; }
    }

    /// <summary>
    /// 数据库迁移配置
    /// </summary>
    public class DbMigrationOptions
    {
        /// <summary>仅报告差异不实际执行（安全模式）。默认 false，即自动应用迁移</summary>
        public bool ReportOnly { get; set; } = false;

        /// <summary>
        /// 额外需要迁移的实体类型（完全限定名，如 "ZR.Model.YourEntity, ZR.Model"）。
        /// 系统实体注册表（37 个框架实体）始终会被迁移，此配置用于扩展用户自定义表。
        /// 类型必须有 [SugarTable] 特性且未标记 [SkipMigration] 才会生效。
        /// </summary>
        public string[] AdditionalTypes { get; set; }
    }

}

