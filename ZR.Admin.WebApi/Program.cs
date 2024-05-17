using AspNetCoreRateLimit;
using Infrastructure.Converter;
using Microsoft.AspNetCore.DataProtection;
using NLog.Web;
using SqlSugar;
using System.Text.Json;
using ZR.Admin.WebApi.Extensions;
using ZR.Common.Cache;
using ZR.Common.DynamicApiSimple.Extens;
using ZR.Infrastructure.WebExtensions;
using ZR.ServiceCore.Signalr;
using ZR.ServiceCore.SqlSugar;

var builder = WebApplication.CreateBuilder(args);
// NLog: Setup NLog for Dependency injection
//builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.Services.AddDynamicApi();
// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//注入HttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
// 跨域配置
builder.Services.AddCors(builder.Configuration);
//消除Error unprotecting the session cookie警告
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DataProtection"));
//普通验证码
builder.Services.AddCaptcha(builder.Configuration);
//IPRatelimit
builder.Services.AddIPRate(builder.Configuration);
//builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
//绑定整个对象到Model上
builder.Services.Configure<OptionsSetting>(builder.Configuration);
builder.Configuration.AddJsonFile("codeGen.json");
builder.Configuration.AddJsonFile("iprate.json");
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

var app = builder.Build();
InternalApp.ServiceProvider = app.Services;
InternalApp.Configuration = builder.Configuration;
InternalApp.WebHostEnvironment = app.Environment;
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
        context.Request.Headers.Add("Authorization", $"Bearer {token}");
    }
    return next();
});
//开启访问静态文件/wwwroot目录文件，要放在UseRouting前面
app.UseStaticFiles();
//开启路由访问
app.UseRouting();
app.UseCors("Policy");//要放在app.UseEndpoints前。
//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

//开启缓存
app.UseResponseCaching();
if (builder.Environment.IsProduction())
{
    //恢复/启动任务
    app.UseAddTaskSchedulers();
}
//初始化字典数据
app.UseInit();

//使用swagger
app.UseSwagger();
//启用客户端IP限制速率
app.UseIpRateLimiting();
app.UseRateLimiter();
//设置socket连接
app.MapHub<MessageHub>("/msgHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();
app.Run();