using AspNetCoreRateLimit;
using Hei.Captcha;
using Infrastructure;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Framework;
using ZR.Admin.WebApi.Hubs;
using ZR.Admin.WebApi.Middleware;

namespace ZR.Admin.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public IConfiguration Configuration { get; }
        public void ConfigureServices(IServiceCollection services)
        {
            string corsUrls = Configuration["corsUrls"];

            //配置跨域
            services.AddCors(c =>
            {
                c.AddPolicy("Policy", policy =>
                {
                    policy.WithOrigins(corsUrls.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .AllowAnyHeader()//允许任意头
                    .AllowCredentials()//允许cookie
                    .AllowAnyMethod();//允许任意方法
                });
            });
            //注入SignalR实时通讯，默认用json传输
            services.AddSignalR(options =>
            {
                //客户端发保持连接请求到服务端最长间隔，默认30秒，改成4分钟，网页需跟着设置connection.keepAliveIntervalInMilliseconds = 12e4;即2分钟
                //options.ClientTimeoutInterval = TimeSpan.FromMinutes(4);
                //服务端发保持连接请求到客户端间隔，默认15秒，改成2分钟，网页需跟着设置connection.serverTimeoutInMilliseconds = 24e4;即4分钟
                //options.KeepAliveInterval = TimeSpan.FromMinutes(2);
            });
            //消除Error unprotecting the session cookie警告
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DataProtection"));
            //普通验证码
            services.AddHeiCaptcha();
            services.AddIPRate(Configuration);
            services.AddSession();
            services.AddMemoryCache();
            services.AddHttpContextAccessor();

            //绑定整个对象到Model上
            services.Configure<OptionsSetting>(Configuration);

            //jwt 认证
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddCookie()
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = JwtUtil.ValidParameters();
            });

            InjectServices(services, Configuration);

            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(GlobalActionMonitor));//全局注册
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonConverterUtil.DateTimeNullConverter());
            });

            services.AddSwaggerConfig();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
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

            //app.UseAuthentication会启用Authentication中间件，该中间件会根据当前Http请求中的Cookie信息来设置HttpContext.User属性（后面会用到），
            //所以只有在app.UseAuthentication方法之后注册的中间件才能够从HttpContext.User中读取到值，
            //这也是为什么上面强调app.UseAuthentication方法一定要放在下面的app.UseMvc方法前面，因为只有这样ASP.NET Core的MVC中间件中才能读取到HttpContext.User的值。
            //1.先开启认证
            app.UseAuthentication();
            //2.再开启授权
            app.UseAuthorization();
            //开启session
            //app.UseSession();
            //开启缓存
            app.UseResponseCaching();
            //恢复/启动任务
            app.UseAddTaskSchedulers();
            //使用全局异常中间件
            app.UseMiddleware<GlobalExceptionMiddleware>();
            //启用客户端IP限制速率
            app.UseIpRateLimiting();

            app.UseEndpoints(endpoints =>
            {
                //设置socket连接
                endpoints.MapHub<MessageHub>("/msgHub");

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        /// <summary>
        /// 注册Services服务
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        private void InjectServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddAppService();
            services.AddSingleton(new AppSettings(configuration));
            //开启计划任务
            services.AddTaskSchedulers();
            //初始化db
            DbExtension.AddDb(configuration);
            
            //注册REDIS 服务
            Task.Run(() =>
            {
                //RedisServer.Initalize();
            });
        }
    }
}
