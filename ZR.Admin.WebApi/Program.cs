using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using ZR.Admin.WebApi.Framework;
using Hei.Captcha;
using Infrastructure.Extensions;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Middleware;
using ZR.Admin.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//注入HttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

//配置跨域
builder.Services.AddCors(c =>
{
    c.AddPolicy("Policy", policy =>
    {
        policy.WithOrigins(builder.Configuration["corsUrls"].Split(',', StringSplitOptions.RemoveEmptyEntries))
        .AllowAnyHeader()//允许任意头
        .AllowCredentials()//允许cookie
        .AllowAnyMethod();//允许任意方法
    });
});

//注入SignalR实时通讯，默认用json传输
builder.Services.AddSignalR();
//消除Error unprotecting the session cookie警告
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DataProtection"));
//普通验证码
builder.Services.AddHeiCaptcha();
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
});

InternalApp.InternalServices = builder.Services;
builder.Services.AddAppService();
builder.Services.AddSingleton(new AppSettings(builder.Configuration));
//开启计划任务
builder.Services.AddTaskSchedulers();
//初始化db
DbExtension.AddDb(builder.Configuration);

//注册REDIS 服务
Task.Run(() =>
{
    //RedisServer.Initalize();
});
builder.Services.AddMvc(options =>
{
    options.Filters.Add(typeof(GlobalActionMonitor));//全局注册
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeConverter());
    options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeNullConverter());
});

builder.Services.AddSwaggerConfig();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//使可以多次多去body内容
app.Use((context, next) =>
{
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
//恢复/启动任务
app.UseAddTaskSchedulers();
//使用全局异常中间件
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseEndpoints(endpoints =>
{
    //设置socket连接
    endpoints.MapHub<MessageHub>("/msgHub");

    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});
app.MapControllers();

app.Run();