using AspNetCoreRateLimit;
using Infrastructure.Converter;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using ZR.Admin.WebApi.Extensions;
using ZR.Common.Cache;
using ZR.Infrastructure.WebExtensions;
using ZR.ServiceCore.Signalr;
using ZR.ServiceCore.SqlSugar;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//注入HttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
// 跨域配置
builder.Services.AddCors(builder.Configuration);
// 显示logo
builder.Services.AddLogo();
//注入SignalR实时通讯，默认用json传输
builder.Services.AddSignalR();
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

//jwt 认证
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddCookie()
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = JwtUtil.ValidParameters();
    o.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            // 如果过期，把过期信息添加到头部
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                Console.WriteLine("jwt过期了");
                context.Response.Headers.Add("Token-Expired", "true");
            }

            return Task.CompletedTask;
        },
    };
});

builder.Services.AddSingleton(new AppSettings(builder.Configuration));
builder.Services.AddAppService();
//开启计划任务
builder.Services.AddTaskSchedulers();

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
    options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
    options.JsonSerializerOptions.WriteIndented = true;
    options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeConverter());
    options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeNullConverter());
    options.JsonSerializerOptions.Converters.Add(new StringConverter());
});

builder.Services.AddSwaggerConfig();

var app = builder.Build();
InternalApp.ServiceProvider = app.Services;
InternalApp.Configuration = builder.Configuration;
InternalApp.WebHostEnvironment = app.Environment;
//初始化db
builder.Services.AddDb(app.Environment);

//使用全局异常中间件
app.UseMiddleware<GlobalExceptionMiddleware>();

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