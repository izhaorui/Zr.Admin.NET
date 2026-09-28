using AspNetCoreRateLimit;
using FluentValidation;
using Infrastructure;
using Infrastructure.Cache;
using Infrastructure.Extensions;
using Infrastructure.Model;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using NLog.Web;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using SqlSugar;
using System.Globalization;
using ZR.Front.WebApi;
using ZR.Infrastructure.IPTools;
using ZR.Infrastructure.WebExtensions;
using ZR.Model;
using ZR.ServiceCore.Middleware;
using ZR.ServiceCore.Oss;
using ZR.ServiceCore.SqlSugar;

// ============================================================================
// 公网 C 端宿主（ZR.Front.WebApi）
// 只暴露被 [PublicApi] 标记的 C 端接口；后台管理接口在本宿主上一律不可达。
// 与内网宿主 ZR.Admin.WebApi 共用同一套数据库/租户/短信/微信支付/令牌配置。
// 本宿主【不】执行任何单实例动作：
//   --initdb 建表与种子、Quartz 定时任务、字典初始化(UseInit)、SignalR /msgHub 映射。
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// NLog：与内网宿主一致
builder.Host.UseNLog();

// ---------------------------------------------------------------------------
// 控制器注册：fail-closed 白名单
// 不能使用 AddDynamicApi()：它会加载输出目录全部 ZR*.dll 并放行所有 ControllerBase，
// 导致后台管理接口被公网暴露。此处只挂 ZR.Mall 程序集，且只放行带 [PublicApi] 的控制器。
// ---------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        // 与内网宿主保持一致：时间格式 + 驼峰命名
        options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
        options.SerializerSettings.ContractResolver =
            new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
    })
    .ConfigureApplicationPartManager(m =>
    {
        m.ApplicationParts.Clear();
        m.ApplicationParts.Add(new AssemblyPart(typeof(ZR.Mall.Model.Product).Assembly));
        m.FeatureProviders.Clear();
        m.FeatureProviders.Add(new PublicControllerFeatureProvider());
    });

// 模型校验：与内网宿主一致，保持既有 C 端请求校验契约
builder.Services.AddFluentValidationAutoValidation(options =>
{
    options.DisableBuiltInModelValidation = false;
});
builder.Services.AddValidatorsFromAssemblies(new[]
{
    typeof(Program).Assembly,
    typeof(PagerInfo).Assembly,
    typeof(ZR.Mall.Model.Product).Assembly
});

builder.Services.AddHttpClient();

// 不做 Swagger：公网宿主不对外暴露接口文档（接口说明见 document/商城会员接口对接文档.md）

// IP 归属地库：必须初始化，否则运行时会在以下路径抛 "IpTool is not initialized"：
//   - 会员发码 MemberService.SendCode / 游客发码 FrontOrderController.SendQueryCode（均经 HttpContextExtension.GetIpInfo）
//   - 全局异常中间件 GlobalExceptionMiddleware（任何异常都会调用 IpTool.Search）
// ip2region_v4.xdb 为本宿主自有文件（见 csproj，随构建复制到输出/发布目录）。
// 仍对「内容根目录」与「运行目录」两处探测，兼容 dotnet run 与从任意工作目录启动已发布的 dll。
var ip2RegionDbPath = new[] { builder.Environment.ContentRootPath, AppContext.BaseDirectory }
    .Select(dir => Path.Combine(dir, "ip2region_v4.xdb"))
    .FirstOrDefault(File.Exists)
    ?? throw new FileNotFoundException("未找到 IP 归属地库 ip2region_v4.xdb，请确认随宿主一起发布（见 ZR.Front.WebApi.csproj 与 Dockerfile）");
builder.Services.AddIP2RegionService(ip2RegionDbPath, cachePolicy: CachePolicy.Content);

// 注入 HttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddHttpContextAccessor();

// 跨域（corsUrls）
builder.Services.AddCors(builder.Configuration);

// DataProtection（与内网宿主一致，避免 cookie 密钥未持久化告警）
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DataProtection"));

// 配置对象与静态配置
builder.Services.Configure<OptionsSetting>(builder.Configuration);
builder.Services.AddSingleton(new AppSettings(builder.Configuration));

// 后台鉴权管线已移除（AddJwt/UseAuthentication/JwtAuthMiddleware 均不注册）：
// 公网宿主全部端点均为 [AllowAnonymous]，不存在 [Authorize] 端点，无需解析后台令牌；
// 也因此公网宿主不持有后台 JWT 签名密钥（JwtSettings:SecretKey 配置为独立占位密钥）。
// 会员身份由 mall-token + MemberAuthorize 过滤器校验（见 ZR.Mall/Common）。

// 业务服务注册（[AppService] 按引用闭包扫描 ZR.* 程序集）
builder.Services.AddAppService();

// 对象存储(OSS)提供程序：SysFileService 等由 AddAppService 自动注册，但其依赖的 IOssProvider
// 不在扫描范围内，必须显式注册（与内网宿主同源；按 OptionsSetting.OssProvider 选择厂商）
builder.Services.AddSingleton<IOssProvider>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OptionsSetting>>();
    var provider = (options.Value.OssProvider ?? "Aliyun").Trim().ToLowerInvariant();
    return provider switch
    {
        "aliyun" => new AliyunOssProvider(options),
        _ => new AliyunOssProvider(options)
    };
});

// SignalR：仅注册服务以提供 IHubContext<MessageHub>
// （SysUserMsgService → IMessageNotifier 依赖它；订单通知会用到）。
// 本宿主【不】调用 MapHub，公网不暴露 /msgHub。
builder.Services.AddSignalR();

// 请求体大小限制（依赖 Upload:requestLimitSize）
builder.Services.AddRequestLimit(builder.Configuration);
// 读取 iprate.json：AddUserRateLimiting 只从传入的 IConfiguration 读 ClientRateLimiting 节点，
// 不自行加载文件；必须先 AddJsonFile，否则规则为空、限流形同未启用（公网宿主尤其需要）。
builder.Configuration.AddJsonFile("iprate.json");
// 接口限流（规则见 iprate.json）——公网必须启用
builder.Services.AddUserRateLimiting(builder.Configuration);

// Redis（与内网宿主一致；RedisServer:open=1 时生效）
if (builder.Configuration["RedisServer:open"] == "1")
{
    RedisServer.Initalize();
}

// 本地化
builder.Services.AddLocalization(options => options.ResourcesPath = "");

var app = builder.Build();

// App.* 全部委托给 InternalApp，必须在 Build 后立即赋值，
// 否则 App.Configuration 为空会导致 MemberJwt 读不到配置、租户解析失效。
InternalApp.ServiceProvider = app.Services;
InternalApp.Configuration = builder.Configuration;
InternalApp.WebHostEnvironment = app.Environment;
IpTool.Configure(app.Services.GetRequiredKeyedService<ISearcher>("IP2Region.Net"));

// SqlSugar 多连接 + 多租户路由（与内网宿主同源）。
// 必须放在 InternalApp 赋值之后：AddDb 内部通过 App.OptionsSetting 读取 DbConfigs。
// checkMigration: false —— 存量库补列/补表属单实例动作，多副本并发执行 DDL 有风险；
// 本宿主不做迁移，由内网宿主启动或 --initdb 负责（前提：内网侧已完成过一次迁移）。
builder.Services.AddDb(app.Environment, checkMigration: false);

// 雪花ID工作号：必须与内网宿主不同，避免两宿主生成重复 ID（见 appsettings.json 的 workId）
var workId = builder.Configuration["workId"].ParseToInt();
if (app.Environment.IsDevelopment())
{
    workId += 1;
}
SnowFlakeSingle.WorkId = workId;

// 全局异常中间件
app.UseMiddleware<GlobalExceptionMiddleware>();

// 本地化
var supportedCultures = new List<CultureInfo> { new("zh-Hant"), new("zh-CN"), new("en") };
app.UseRequestLocalization(options =>
{
    options.DefaultRequestCulture = new RequestCulture("zh-CN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = true;
});

// 公网部署必需：解析 X-Forwarded-For / X-Forwarded-Proto，取真实客户端 IP（限流与审计依赖）
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseRouting();
app.UseCors("Policy");

// 多租户解析：匿名 C 端请求按访问域名定位租户库
app.UseMiddleware<TenantResolveMiddleware>();
app.UseMiddleware<TenantFeatureMiddleware>();

app.UseResponseCaching();
app.UseClientRateLimiting();

app.MapControllers();

app.Run();
