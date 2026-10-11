using Infrastructure.Enums;
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
        /// 模块初始化开关：决定 --initdb 时哪些模块纳入初始化（建表 / 模块内置定时任务 / 模块菜单种子）。
        /// 与运行期功能开关（TenantSettings.UseTenant、AiOptions.Enable）相互独立。
        /// </summary>
        public ModuleInitOptions ModuleInit { get; set; } = new();
        /// <summary>
        /// 数据库迁移配置（自动发现实体、差异检测、迁移历史）
        /// </summary>
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
        /// 当前启用的对象存储(OSS)提供程序，可选：s3 / aliyun，默认 Aliyun。
        /// s3     → S3CompatibleOssProvider（S3 兼容服务：RustFS / MinIO / Ceph RGW / R2 等，读 S3 配置节）
        /// aliyun → AliyunOssProvider（上传功能尚未实现，见该类注释）
        /// </summary>
        public string OssProvider { get; set; } = "Aliyun";

        /// <summary>
        /// 阿里云oss
        /// </summary>
        public ALIYUN_OSS ALIYUN_OSS { get; set; }

        /// <summary>
        /// S3 兼容对象存储（RustFS / MinIO / Ceph RGW / R2 等）
        /// </summary>
        public S3Compatible S3 { get; set; }
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
        /// <summary>
        /// AI 配置
        /// </summary>
        public AiOptions AiOptions { get; set; }
        /// <summary>
        /// 工作流配置
        /// </summary>
        public WorkflowOptions Workflow { get; set; } = new WorkflowOptions();
    }

    /// <summary>
    /// 模块初始化开关：决定 --initdb 时哪些模块纳入初始化（建表 / 模块内置定时任务 / 模块菜单种子）。
    /// 全部默认 false，按部署实际启用的模块开启；与运行期功能开关
    /// （TenantSettings.UseTenant、AiOptions.Enable）相互独立，互不推导。
    /// </summary>
    public class ModuleInitOptions
    {
        /// <summary>
        /// 初始化 SaaS 平台级菜单与权限（租户管理、套餐菜单、字典种子等）。无独立业务建表。
        /// </summary>
        public bool Saas { get; set; }

        /// <summary>
        /// 初始化会员能力：建会员表 ums_member（与商城同库 MallDb）+ 自增种子校正。
        /// 会员身份为平台级通用能力（与 sys_user 对等），与商城/内容/社交等业务模块相互独立。
        /// </summary>
        public bool Member { get; set; }

        /// <summary>
        /// 初始化商城模块：建商城业务表 + 商城内置定时任务 + 商城菜单。
        /// </summary>
        public bool Mall { get; set; }

        /// <summary>
        /// 初始化工作流模块：建工作流业务表 + 工作流内置定时任务 + 工作流菜单。
        /// </summary>
        public bool Workflow { get; set; }

        /// <summary>
        /// 初始化专业版(AI)菜单与按钮权限。AI 功能本身由 AiOptions.Enable 控制，两者独立。
        /// </summary>
        public bool Pro { get; set; }
    }

    /// <summary>
    /// 工作流模块配置
    /// </summary>
    public class WorkflowOptions
    {
        /// <summary>
        /// 通知渠道配置（站内信始终发送；邮件/短信按此开关额外分发）
        /// </summary>
        public WorkflowNotifyOptions Notify { get; set; } = new WorkflowNotifyOptions();
    }
    /// <summary>
    /// 工作流通知渠道开关。外部渠道仅对"行动类"通知生效（待办/催办/转办/委托/加签/改派等需要用户处理的消息），
    /// 进度/撤回/终止等知会类通知不发邮件短信，避免打扰。
    /// </summary>
    public class WorkflowNotifyOptions
    {
        /// <summary>是否通过邮件分发行动类通知（需先配置 MailOptions 发件邮箱）</summary>
        public bool EmailEnabled { get; set; }
        /// <summary>是否通过短信分发行动类通知（真实发送还需 SmsOptions.Enabled=true 并配置 workflow 模板）</summary>
        public bool SmsEnabled { get; set; }
        /// <summary>工作流通知邮件使用的发件人配置（对应 MailOptions 数组中的 FromName，取第一个匹配项；为空用第一项）</summary>
        public string MailFromName { get; set; } = "system";
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
        public string[] NotAllowedExt { get; set; } = [];

        /// <summary>
        /// 默认存储类型：调用方未显式传 storeType 时使用（配置值写 Local / ALIYUN / S3）。
        /// 显式传入的 storeType 优先，这里只决定"没传时去哪"。
        /// 想让商品图等所有业务上传都进对象存储，改这一项即可，无需逐个改前端上传入口。
        /// </summary>
        public StoreType DefaultStoreType { get; set; } = StoreType.LOCAL;
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
    /// S3 兼容对象存储（RustFS / MinIO / Ceph RGW / Cloudflare R2 / Backblaze B2 / AWS S3 等）
    /// </summary>
    public class S3Compatible
    {
        /// <summary>展示与日志用名称，如 RustFS / MinIO</summary>
        public string DisplayName { get; set; } = "S3";

        /// <summary>服务端点，例：http://192.168.1.10:9000；云厂商形如 https://s3.cn-north-1.amazonaws.com.cn</summary>
        public string Endpoint { get; set; }

        public string AccessKey { get; set; }

        public string SecretKey { get; set; }

        public string BucketName { get; set; }

        /// <summary>
        /// 可公开读取的访问域名，用于拼接 SysFile.AccessUrl。
        /// 必须保证浏览器 / 小程序能直接访问（bucket 公开读或反向代理 + HTTPS），否则前端图片 403。
        /// 留空时 GetAccessUrl 降级返回对象键（相对路径）。
        /// </summary>
        public string DomainUrl { get; set; }

        /// <summary>
        /// 路径风格寻址（http://ip:port/bucket/key）。
        /// 自建 S3 兼容服务必须为 true —— 否则 SDK 按 Virtual-host 拼成 bucket.ip:port，域名无法解析。
        /// </summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>SDK 签名必填项，自建服务通常忽略该值</summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>单文件大小上限(MB)</summary>
        public int MaxSize { get; set; } = 100;

        /// <summary>
        /// 对象缓存头（Cache-Control），随上传写入对象元数据。
        /// 作用是让浏览器 / Nginx / CDN 缓存图片，避免每次浏览都重新下载 —— 这是自建存储省流量的关键一项。
        /// 留空则不发送该头（即不缓存）。
        /// 注意：若对象键可能被覆盖（同名文件重复上传），应缩短 max-age，否则用户会长时间看到旧图；
        /// 默认 30 天是基于"对象键含日期目录 + 随机文件名、几乎不会覆盖"的前提。
        /// </summary>
        public string CacheControl { get; set; } = "public, max-age=2592000";
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

    public class AiOptions
    {
        /// <summary>
        /// 是否启用 AI
        /// </summary>
        public bool Enable { get; set; }

        /// <summary>
        /// 提供商：openai / deepseek / qwen
        /// </summary>
        public string Provider { get; set; } = "openai";

        /// <summary>
        /// OpenAI 兼容接口基地址，如 https://api.openai.com
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// 聊天接口路径，如 /v1/chat/completions。
        /// 注意：留空才能命中 Providers 分项配置（分项匹配优先级高于顶层默认值），
        /// 否则 qwen/dashscope 等 BaseUrl 已含 /v1 的提供商会把路径拼成 /v1/v1/chat/completions。
        /// </summary>
        public string ChatEndpoint { get; set; }

        /// <summary>
        /// 模型名称，如 gpt-4o-mini / deepseek-chat / qwen-turbo
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// API Key。优先用 Providers 分项里的 ApiKey，这里是未匹配到分项时的兜底。
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// 温度参数
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// 最大输出 token
        /// </summary>
        public int MaxTokens { get; set; } = 1024;

        /// <summary>
        /// 请求超时秒
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// 是否开启思考模式（reasoning）。仅对支持 enable_thinking 的混合思考模型
        /// （如 Qwen3 系列 qwen3.7-plus 等）生效，显式传给请求体；true 开启、false 关闭。
        /// 关闭思考可显著降低首字延迟、减少 token 消耗。默认 false。
        /// </summary>
        public bool EnableThinking { get; set; } = false;

        /// <summary>
        /// 提供商配置列表（用于不同provider使用不同ApiKey）
        /// </summary>
        public List<AiProviderOptions> Providers { get; set; }

        /// <summary>
        /// 提示词（Prompt）目录。工作流 AI 的各能力提示词以 .md 文件存放在此目录，运行时读取、可热改无需重编译。
        /// 留空则默认取应用程序基目录下的 Prompts 子目录（AppContext.BaseDirectory/Prompts）。
        /// 文件缺失不会阻断编译或启动，仅会在对应能力调用时抛出友好异常。
        /// </summary>
        public string PromptDir { get; set; }

        /// <summary>
        /// 视觉模型 Provider。留空则用当前文本 Provider（顶层 Provider）。
        /// 仅当看图要走另一家供应商时才填。
        /// </summary>
        public string VisionProvider { get; set; }

        /// <summary>
        /// 视觉接口基地址覆盖。留空则用目标 Provider 的 BaseUrl / 默认地址。
        /// </summary>
        public string VisionBaseUrl { get; set; }

        /// <summary>
        /// 视觉聊天接口路径覆盖。留空则用目标 Provider 的 ChatEndpoint / 默认路径。
        /// </summary>
        public string VisionChatEndpoint { get; set; }

        /// <summary>
        /// 视觉模型覆盖。留空则用目标 Provider 的 <see cref="AiProviderOptions.VisionModel"/>。
        /// 都为空表示未开通看图。
        /// </summary>
        public string VisionModel { get; set; }

        /// <summary>
        /// 视觉 API Key 覆盖。留空则用目标 Provider 的 ApiKey。
        /// </summary>
        public string VisionApiKey { get; set; }

        /// <summary>
        /// AI 附件下载/发图域名白名单（SSRF 与内网信息泄露防护）。
        /// 留空=不限制（默认，兼容内网自托管附件部署）；配置后仅允许从这些 host
        /// 下载附件文本或把图片 URL 发给视觉模型，如 ["upload.example.com"]。
        /// </summary>
        public List<string> AttachmentDownloadAllowedHosts { get; set; }

        /// <summary>
        /// 单用户每分钟 AI 助手（aichat）最多发起的对话消息数；0=不限制。
        /// 防止脚本/被盗账号高频刷 LLM 造成费用与资源浪费（按用户计，非 IP）。
        /// </summary>
        public int ChatRateLimitPerMinute { get; set; } = 20;

        /// <summary>
        /// 单用户当月累计消耗 token 上限（自然月，每月 1 日重置）；0=不限制。
        /// 兑现配置注释原意：达阈值后拒绝该用户继续发起 AI 对话，直到次月重置。
        /// 按 ai_call_log(Scene='ai_chat') 记账累计。
        /// </summary>
        public long DefaultUserTotalTokens { get; set; } = 2000000;

        /// <summary>
        /// 是否启用自动重试。当 true 时，对网络抖动、限流等可恢复错误自动重试。默认 true
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// 最大重试次数（不含首次请求）。默认 3 次。禁用重试时可设为 0
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 首次重试延迟（毫秒）。默认 500ms。指数退避会逐次翻倍，直至 MaxRetryDelayMs
        /// </summary>
        public int InitialRetryDelayMs { get; set; } = 500;

        /// <summary>
        /// 最大重试延迟（毫秒）。防止重试等待过长。默认 30000ms (30 秒)
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 30000;
    }

    public class AiProviderOptions
    {
        /// <summary>
        /// provider标识，如 openai/deepseek/qwen
        /// </summary>
        public string Provider { get; set; }

        /// <summary>下拉展示名，如 通义千问。留空则显示 Provider 标识。</summary>
        public string Label { get; set; }

        public string BaseUrl { get; set; }

        public string ChatEndpoint { get; set; }

        public string Model { get; set; }

        /// <summary>
        /// 该 Provider 的看图模型（须支持多模态）。与文本 Model 分离，共用本项 BaseUrl / ApiKey。
        /// 留空表示此供应商不开通看图。
        /// </summary>
        public string VisionModel { get; set; }

        /// <summary>该 Provider 可出现在下拉中的模型建议；真正发请求仍用 Model。</summary>
        public List<string> Models { get; set; }

        public string ApiKey { get; set; }

        public double? Temperature { get; set; }

        public int? MaxTokens { get; set; }

        public int? TimeoutSeconds { get; set; }
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

