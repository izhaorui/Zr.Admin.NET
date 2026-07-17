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
using ZR.Common.Cache;
using ZR.Common.DynamicApiSimple.Extens;
using ZR.Infrastructure.IPTools;
using ZR.Infrastructure.WebExtensions;
using ZR.Model;
using ZR.ServiceCore.Signalr;
using ZR.ServiceCore.SqlSugar;

//using SQLitePCL;

var builder = WebApplication.CreateBuilder(args);
// NLog: Setup NLog for Dependency injection
//builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddDynamicApi();
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
    typeof(ZR.Mall.Model.Product).Assembly
});

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
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DataProtection"));
//普通验证码
builder.Services.AddCaptchaProvider();
// 读取额外配置文件（iprate.json 要在注册限流服务之前加载）
builder.Configuration.AddJsonFile("iprate.json");
//IPRatelimit
builder.Services.AddIPRate(builder.Configuration);
//builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
//绑定整个对象到Model上
builder.Services.Configure<OptionsSetting>(builder.Configuration);
builder.Configuration.AddJsonFile("codeGen.json");
//jwt 认证
builder.Services.AddJwt();
//配置文件
builder.Services.AddSingleton(new AppSettings(builder.Configuration));
//app服务注册
builder.Services.AddAppService();
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
var workId = builder.Configuration["workId"].ParseToInt();
if (app.Environment.IsDevelopment())
{
    workId += 1;
}
SnowFlakeSingle.WorkId = workId;
//使用全局异常中间件
app.UseMiddleware<GlobalExceptionMiddleware>();

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
    if (context.Request.Query.TryGetValue("access_token", out var token))
    {
        context.Request.Headers.Append("Authorization", $"Bearer {token}");
    }
    return next();
});
//开启访问静态文件/wwwroot目录文件，要放在UseRouting前面
app.UseStaticFiles();
//开启路由访问
app.UseRouting();
app.UseCors("Policy");//要放在app.UseEndpoints前。
//app.UseHttpsRedirection();

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
if (builder.Environment.IsProduction())
{
    //恢复/启动任务
    await app.UseAddTaskSchedulers();
}
//初始化字典数据
app.UseInit();

//swagger 只在开发环境中使用
if (builder.Environment.IsDevelopment())
{
    app.UseSwagger();
}

//启用客户端IP限制速率
app.UseIpRateLimiting();
app.UseRateLimiter();
//设置socket连接
app.MapHub<MessageHub>("/msgHub");

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.Run();