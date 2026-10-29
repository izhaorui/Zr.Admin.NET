using AspNetCoreRateLimit;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Infrastructure.Converter;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using NLog.Web;
using SqlSugar;
using System.Globalization;
using System.Text.Json;
using ZR.Admin.WebApi.Extensions;
using Infrastructure.Cache;
using ZR.Common.DynamicApiSimple.Extens;
using ZR.ServiceCore.Email;
using Infrastructure.Oss;
using ZR.Oss;
using ZR.Infrastructure.IPTools;
using ZR.Infrastructure.WebExtensions;
using ZR.Model;
using ZR.ServiceCore.Signalr;
using ZR.ServiceCore.SqlSugar;
using Infrastructure.Captcha;

var builder = WebApplication.CreateBuilder(args);
// NLog: Setup NLog for Dependency injection
//builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddDynamicApi();
// 内网宿主不暴露任何 C 端接口：约定 [PublicApi] 只归公网宿主 ZR.Front.WebApi。
// 被移除的控制器不会生成路由/endpoint，也不进 Swagger，因此本宿主无需 MemberJwt 配置。
builder.Services.ExcludePublicApiControllers();
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation(options =>
{
    // Keep Microsoft DataAnnotations validation for backward compatibility.
    options.DisableBuiltInModelValidation = false;
});
builder.Services.AddValidatorsFromAssemblies(new[]
{
    typeof(Program).Assembly,
    typeof(PagerInfo).Assembly,
    typeof(ZR.Mall.Model.Product).Assembly,
    typeof(ZR.Workflow.Model.WfFlowDefinition).Assembly
});

builder.Services.AddHttpClient();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var ip2RegionDbPath = Path.Combine(builder.Environment.ContentRootPath, "ip2region_v4.xdb");
builder.Services.AddIP2RegionService(ip2RegionDbPath, cachePolicy: CachePolicy.Content);

//注入HttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
// 跨域配置
builder.Services.AddCors(builder.Configuration);
//消除Error unprotecting the session cookie警告
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
//普通验证码
builder.Services.AddCaptchaProvider();
// 读取额外配置文件（iprate.json 要在注册限流服务之前加载）
builder.Configuration.AddJsonFile("iprate.json");
// AI 独立配置：ai.json < ai.{Environment}.json < UserSecrets < 环境变量 < 命令行
// CreateBuilder 之后再 AddJsonFile 会盖掉已加载的 UserSecrets/环境变量，因此这里补挂回去。
builder.Configuration.AddJsonFile("ai.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"ai.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
// 代码生成独立配置：codeGen.json < appsettings.{Environment}.json < UserSecrets < 环境变量 < 命令行
// 必须放在下方 UserSecrets/环境变量/命令行之前加载，否则本文件会反过来盖掉环境变量（CodeGen__vuePath 等将失效）。
builder.Configuration.AddJsonFile("codeGen.json", optional: true, reloadOnChange: true);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
//接口限流（客户端模式：登录用户优先，匿名回退按 IP，规则见 iprate.json）
builder.Services.AddUserRateLimiting(builder.Configuration);
//builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
//绑定整个对象到Model上
builder.Services.Configure<OptionsSetting>(builder.Configuration);
//jwt 认证
builder.Services.AddJwt();
//配置文件
builder.Services.AddSingleton(new AppSettings(builder.Configuration));
//app服务注册
builder.Services.AddAppService();
// 邮件后台派发：队列消费 + 失败退避重试 + 启动恢复（HostedService 不在 [AppService] 扫描范围）
builder.Services.AddEmailDispatch();
// 对象存储(OSS)提供程序：注册工厂 + 默认实现
//   上传接口按调用方传入的 StoreType 取实现（2=阿里云 / 6=S3 兼容），统一由 IOssProviderFactory 路由 ——
//   存储类型是"文件存到哪里"的选择，必须与实现一致，不能由全局配置单方面决定。
//   IOssProvider（默认实现）取 OptionsSetting.OssProvider，仅作兜底与兼容历史调用方。
builder.Services.AddSingleton<IOssProviderFactory, OssProviderFactory>();
builder.Services.AddSingleton<IOssProvider>(sp =>
    sp.GetRequiredService<IOssProviderFactory>().GetDefault());
//开启计划任务
builder.Services.AddTaskSchedulers();
//请求大小限制
builder.Services.AddRequestLimit(builder.Configuration);
//sqlite 包需要的驱动
//Batteries_V2.Init();
//注册REDIS 服务
var openRedis = builder.Configuration["RedisServer:open"];
if (openRedis == "1")
{
    RedisServer.Initalize();
}

builder.Services.AddMvc(options =>
{
    options.Filters.Add(typeof(GlobalActionMonitor));//全局注册
})
.AddJsonOptions(options =>
{
    //options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeConverter());
    options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeNullConverter());
    options.JsonSerializerOptions.Converters.Add(new StringConverter());
    //PropertyNamingPolicy属性用于前端传过来的属性的格式策略，目前内置的仅有一种策略CamelCase
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    //options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;//属性可以忽略大小写格式，开启后性能会降低
});
//注入SignalR实时通讯，默认用json传输
builder.Services.AddSignalR()
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddSwaggerConfig();
// 显示logo
builder.Services.AddLogo();
// 添加本地化服务
builder.Services.AddLocalization(options => options.ResourcesPath = "");
// 在应用程序启动的最开始处调用
var app = builder.Build();
InternalApp.ServiceProvider = app.Services;
InternalApp.Configuration = builder.Configuration;
InternalApp.WebHostEnvironment = app.Environment;
IpTool.Configure(app.Services.GetRequiredKeyedService<ISearcher>("IP2Region.Net"));
//初始化db
builder.Services.AddDb(app.Environment);

// CLI 模式：显式触发全量数据库初始化（迁移 + 种子），完成后直接退出，不启动 Web。
// 用法：dotnet run -- --initdb   或   dotnet ZR.Admin.WebApi.dll --initdb
// 这样 Web 正常启动不再耦合初始化动作，部署时按需手动执行一次即可。
var initDbRequested = args.Any(a => a == "--initdb" || a == "-i");
if (initDbRequested)
{
    InitTable.RunInitDb(app.Environment);
    return;
}
var workId = builder.Configuration["workId"].ParseToInt();
if (app.Environment.IsDevelopment())
{
    workId += 1;
}
SnowFlakeSingle.WorkId = workId;

// 启动自检：SecretKey 需由环境配置文件/环境变量注入，且 HS256 要求不少于 32 字节（256 位），
// 否则令牌签发/校验失败（缺失时 JwtUtil 抛 "JwtSettings获取失败"，过短时 IdentityModel 抛 IDX10720）。
// 只告警不阻断，日志中不含密钥本身。JwtUtil 用 ASCII 取字节，字符数即字节数。
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"] ?? string.Empty;
if (jwtSecretKey.Length < 32)
{
    app.Logger.LogWarning(
        $"JwtSettings:SecretKey 未配置或长度不足（当前 {jwtSecretKey.Length} 字节），HS256 要求不少于 32 字节。" +
        "请通过 appsettings.Development.json / appsettings.Production.json（均已被 gitignore）或环境变量 JwtSettings__SecretKey 注入长度 ≥32 的随机字符串。");
}

// 配置中间件以支持本地化
var supportedCultures = new List<CultureInfo> {
                    new CultureInfo("zh-Hant"),
                    new CultureInfo("zh-CN"),
                    new CultureInfo("en")
                };
app.UseRequestLocalization(options =>
{
    options.DefaultRequestCulture = new RequestCulture("zh-CN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = true;
});

//请求头转发
//ForwardedHeaders中间件会自动把反向代理服务器转发过来的X-Forwarded-For（客户端真实IP）以及X-Forwarded-Proto（客户端请求的协议）自动填充到HttpContext.Connection.RemoteIPAddress和HttpContext.Request.Scheme中，这样应用代码中读取到的就是真实的IP和真实的协议了，不需要应用做特殊处理。
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.Use((context, next) =>
{
    //设置可以多次获取body内容
    context.Request.EnableBuffering();

    // 仅对 SignalR（/msgHub）把 query 里的 access_token 转成 Authorization 头。
    // 为什么需要：WebSocket/SSE 传输无法自定义请求头，signalr JS 客户端的 accessTokenFactory
    //            只能把令牌放在 ?access_token=，而 JwtAuthMiddleware 只读 Authorization 头。
    // 为什么必须限定路径：query 里的令牌会落进访问日志、反向代理日志、浏览器历史，
    //            还可能通过 Referer 泄给第三方站点。其余接口一律不接受 query 传令牌。
    if (context.Request.Path.StartsWithSegments("/msgHub", StringComparison.OrdinalIgnoreCase))
    {
        var accessToken = context.Request.Query["access_token"].ToString();
        // 已有 Authorization 头时以它为准，避免 query 里的值覆盖/拼接出非法头
        if (!string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(context.Request.Headers.Authorization))
        {
            context.Request.Headers.Authorization = $"Bearer {accessToken}";
        }
    }
    return next();
});
//开启访问静态文件/wwwroot目录文件，要放在UseRouting前面
app.UseStaticFiles();
//开启路由访问
app.UseRouting();
app.UseCors("Policy");//要放在app.UseEndpoints前。
//app.UseHttpsRedirection();
//使用全局异常中间件
// 必须放在 UseRouting/UseCors 之后：中间件内部通过 IEndpointFeature 取 endpoint 上的 [LogAttribute]，
// 注册过早会取不到 endpoint，导致异常操作日志的 Title/BusinessType/请求参数开关全部丢失。
// 放在 UseAuthentication 之前，可同时捕获鉴权/租户中间件的异常并统一返回格式。
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseMiddleware<TenantResolveMiddleware>();
//启用认证中间件（必须在 UseAuthorization 之前）
app.UseAuthentication();
//把 Jwt 校验后创建的 ClaimsPrincipal 挂到 HttpContext.Use
app.UseMiddleware<JwtAuthMiddleware>();
//租户套餐功能开关拦截
app.UseMiddleware<TenantFeatureMiddleware>();
app.UseAuthorization();

//开启缓存
app.UseResponseCaching();
// 定时任务开关：默认关闭，按部署环境显式开启（QuartzTask:Enabled 或环境变量 QuartzTask__Enabled=true）。
// 不用 Environment 判断：Staging 等非 Production 环境会漏跑；本地想调试任务又得改环境变量，
// 而 ASPNETCORE_ENVIRONMENT=Production 会连带关掉下面的 Swagger。
if (app.Configuration.GetValue("QuartzTask:Enabled", false))
{
    //恢复/启动任务（异常在扩展方法内收口：查库失败或单任务损坏只记日志，不阻断站点启动）
    await app.UseAddTaskSchedulers();
}
//初始化字典数据
app.UseInit();

//swagger 只在开发环境中使用
if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
}

//启用接口限制速率（客户端模式：登录用户优先，匿名回退按 IP）
app.UseClientRateLimiting();
//设置socket连接
app.MapHub<MessageHub>("/msgHub");

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.Run();