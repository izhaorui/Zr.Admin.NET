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
        /// 初始化专业版菜单与按钮权限：AI 能力按钮/菜单 +「系统管理-应用接入」(SSO)。
        /// AI 功能本身由 AiOptions.Enable 控制，两者独立。
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

        /// <summary>
        /// 是否启用真实发送。false 时为模拟发送（只记录日志/发送记录，不连 SMTP），
        /// 用于开发环境避免误发；默认 true，保持历史行为。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 安全连接方式：Auto / StartTls / StartTlsWhenAvailable / SslOnConnect / None。
        /// 留空时按 UseSsl 自动映射（true→Auto，false→None）：465 端口自动 SSL 直连，587 端口自动 STARTTLS。
        /// </summary>
        public string Security { get; set; }

        /// <summary>连接/读写超时（秒），默认 30，最大 600。</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 是否跳过 SMTP 服务器证书校验。仅内网自签证书场景开启；
        /// 默认 false（走系统证书校验，避免中间人风险）。
        /// </summary>
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>单封邮件附件总大小上限（MB），默认 20；&lt;=0 表示不限制。</summary>
        public int MaxAttachmentMB { get; set; } = 20;

        /// <summary>发送失败后的自动重试次数（不含首次发送），默认 3。</summary>
        public int MaxRetryCount { get; set; } = 3;
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

        /// <summary>
        /// 允许上传的扩展名白名单（含点，如 .png），不区分大小写。
        /// 空数组表示不启用白名单，仅使用 NotAllowedExt 黑名单（保持历史行为）。
        /// </summary>
        public string[] AllowedExt { get; set; } = [];

        /// <summary>
        /// 是否校验文件头（魔数）与扩展名是否一致，默认开启。
        /// 用于拦截「HTML/脚本改名成 .png」这类只改扩展名的绕过方式。
        /// </summary>
        public bool CheckFileSignature { get; set; } = true;

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
        /// 签名密钥（HS256 要求密钥不少于 32 字节/256 位，不足时签发会抛 IdentityModel 的 IDX10720）。
        /// JwtUtil 用 Encoding.ASCII 取字节，字符数即字节数，故实际要求为不少于 32 个字符，且建议只用 ASCII 字符。
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
        /// 会话绝对最长存活时间（分）：无论续期多少次，超过该时长一律强制重新登录。
        /// 0 或负数表示不限制（不建议）；应大于等于 <see cref="Expire"/>。默认 7 天。
        /// </summary>
        public int MaxLifetime { get; set; } = 10080;
        /// <summary>
        /// token类型
        /// </summary>
        public string TokenType { get; set; } = "Bearer";
    }

    /// <summary>
    /// 双因子认证（TOTP）配置。对应 appsettings 的 TwoFactor 节点，全部可缺省。
    /// </summary>
    public class TwoFactorSettings
    {
        /// <summary>
        /// 认证器 App 中显示的发行方名称
        /// </summary>
        public string Issuer { get; set; } = "ZRAdmin";

        /// <summary>
        /// TOTP 密钥的加密口令（AES-GCM 密钥派生源）。
        /// 留空时回退用 JwtSettings:SecretKey 派生，保证开箱即用。
        /// 注意：一旦有用户绑定 2FA 后不要修改该值，否则历史密钥无法解密（用户需重新绑定）。
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// "记住此设备"的信任天数；小于等于 0 时按 7 天处理
        /// </summary>
        public int TrustDeviceDays { get; set; } = 7;

        /// <summary>
        /// 验证码校验失败最大次数，超出后按 <see cref="LockMinutes"/> 锁定
        /// </summary>
        public int MaxFailCount { get; set; } = 5;

        /// <summary>
        /// 验证失败锁定时长（分钟）
        /// </summary>
        public int LockMinutes { get; set; } = 15;

        /// <summary>
        /// 一步登录后的一次性票据有效期（分钟），超时需重新输入密码
        /// </summary>
        public int TicketMinutes { get; set; } = 5;

        /// <summary>
        /// 2FA 连续失败（达到 <see cref="MaxFailCount"/> 触发锁定时）是否发站内信告警。
        /// 复用既有"系统消息"类型，与异地登录、账号锁定提醒一致。
        /// </summary>
        public bool AlertBySiteMessage { get; set; } = true;

        /// <summary>
        /// 是否同时发邮件告警。需先在 MailOptions 配好发件邮箱（FromEmail + Password），
        /// 未配置时静默跳过，只发站内信 —— 与工作流通知同一口径。
        /// </summary>
        public bool AlertByEmail { get; set; } = true;

        /// <summary>
        /// 告警邮件使用的发件人（对应 MailOptions 数组中的 FromName；为空或未匹配时取第一项）
        /// </summary>
        public string AlertMailFromName { get; set; } = "system";

        /// <summary>
        /// 同一账号的告警最小间隔（分钟），防止攻击者靠反复试错把用户邮箱刷爆。
        /// 小于等于 0 时按 60 分钟。
        /// </summary>
        public int AlertIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// 是否在"新设备登录"时发提醒（渠道随 <see cref="AlertBySiteMessage"/> / <see cref="AlertByEmail"/>）。
        /// 首次出现某设备标识即视为新设备。
        /// </summary>
        public bool AlertOnNewDevice { get; set; } = true;

        /// <summary>
        /// 每个账号保留的登录设备记录上限，超出时按最后使用时间淘汰最旧的非信任设备。
        /// 设备标识由客户端提供、可被伪造，必须有上限，否则存在刷表风险。小于等于 0 时按 20 条。
        /// </summary>
        public int MaxDeviceRecords { get; set; } = 20;

        #region WebAuthn / Passkey

        /// <summary>
        /// 是否启用 WebAuthn / Passkey 作为第二因子。
        /// 必须同时配置 <see cref="WebAuthnOrigins"/> 才真正生效：未配置时整体禁用（fail-closed），
        /// 避免"以为开了、实际校验不了 Origin"的半开状态。
        /// </summary>
        public bool WebAuthnEnabled { get; set; } = true;

        /// <summary>
        /// 允许的 Origin 列表（含协议，如 https://admin.example.com）。
        ///
        /// **必须显式配置**：Origin 是 WebAuthn 的核心安全边界，绝不能从请求头推导 ——
        /// 否则攻击者伪造 Host 与 Origin 一致即可绕过校验，防钓鱼的意义归零。
        /// 未配置时 WebAuthn 功能整体关闭。
        /// </summary>
        public List<string> WebAuthnOrigins { get; set; } = new();

        /// <summary>
        /// 依赖方 ID（RP ID），必须是 <see cref="WebAuthnOrigins"/> 中域名的可注册域后缀。
        /// 留空时从 Origins 第一项推导（去掉协议与端口）；子域名共用同一 RP 时配根域名即可。
        /// </summary>
        public string WebAuthnRpId { get; set; }

        /// <summary>
        /// 依赖方名称（认证器弹窗中显示），留空时取 <see cref="Issuer"/>
        /// </summary>
        public string WebAuthnRpName { get; set; }

        /// <summary>
        /// 注册/认证 challenge 有效期（分钟）。应不短于用户完成一次生物识别所需时间，
        /// 但也不宜过长：challenge 一次性，过期后需重新取。
        /// </summary>
        public int WebAuthnChallengeMinutes { get; set; } = 5;

        /// <summary>
        /// 每个账号最多可绑定的凭据数量。凭据由已登录用户自助添加，必须有上限防刷表。
        /// 小于等于 0 时按 10 条。
        /// </summary>
        public int WebAuthnMaxCredentials { get; set; } = 10;

        #endregion
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
        /// 请求超时秒。语义按调用形态区分：
        /// <para>非流式：整个请求的总时长上限。</para>
        /// <para>流式(SSE)：<b>空闲超时</b>——连续这么久没收到任何数据才判定超时，
        /// 只要模型还在持续返回就不会被中断（长回答不会被误杀）。整段流式输出另受
        /// <see cref="MaxStreamDurationSeconds"/> 的硬上限兜底。</para>
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// 流式(SSE)响应的总时长硬上限（秒）。与 <see cref="TimeoutSeconds"/> 的空闲超时配合：
        /// 空闲超时管"多久没数据就失败"，本项管"整段输出最长跑多久"，防止服务端以"慢速滴流"
        /// 方式长期占用连接与治理在途额度。默认 600 秒（10 分钟）；0 = 不限制。
        /// </summary>
        public int MaxStreamDurationSeconds { get; set; } = 600;

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

