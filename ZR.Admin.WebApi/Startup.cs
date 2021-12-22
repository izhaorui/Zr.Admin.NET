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
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.OpenApi.Models;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Framework;
using ZR.Admin.WebApi.Middleware;

namespace ZR.Admin.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment hostEnvironment)
        {
            Configuration = configuration;
            CurrentEnvironment = hostEnvironment;
        }
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private IWebHostEnvironment CurrentEnvironment { get; }
        public IConfiguration Configuration { get; }
        public void ConfigureServices(IServiceCollection services)
        {
            string corsUrls = Configuration["sysConfig:cors"];

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
            //消除Error unprotecting the session cookie警告
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DataProtection"));
            //普通验证码
            services.AddHeiCaptcha();
            services.AddSession();
            services.AddHttpContextAccessor();


            //绑定整个对象到Model上
            services.Configure<OptionsSetting>(Configuration);
            services.Configure<JwtSettings>(Configuration);
            var jwtSettings = new JwtSettings();
            Configuration.Bind("JwtSettings", jwtSettings);

            //Cookie 认证
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddCookie()
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = JwtUtil.ValidParameters();
            });

            InjectServices(services);

            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(GlobalActionMonitor));//全局注册异常
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
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "swagger/{documentName}/swagger.json";
                c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                {
                    var url = $"{httpReq.Scheme}://{httpReq.Host.Value}";
                    var referer = httpReq.Headers["Referer"].ToString();
                    if (referer.Contains(GlobalConstant.DevApiProxy))
                        url = referer.Substring(0,
                            referer.IndexOf(GlobalConstant.DevApiProxy, StringComparison.InvariantCulture) + GlobalConstant.DevApiProxy.Length - 1);
                    swaggerDoc.Servers =
                        new List<OpenApiServer>
                        {
                            new OpenApiServer
                            {
                                Url = url
                            }
                        };
                });
            });
            app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "ZrAdmin v1"));

            //使可以多次多去body内容
            app.Use((context, next) =>
            {
                context.Request.EnableBuffering();
                return next();
            });
            //开启访问静态文件/wwwroot目录文件，要放在UseRouting前面
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCors("Policy");//要放在app.UseEndpoints前。

            //app.UseAuthentication会启用Authentication中间件，该中间件会根据当前Http请求中的Cookie信息来设置HttpContext.User属性（后面会用到），
            //所以只有在app.UseAuthentication方法之后注册的中间件才能够从HttpContext.User中读取到值，
            //这也是为什么上面强调app.UseAuthentication方法一定要放在下面的app.UseMvc方法前面，因为只有这样ASP.NET Core的MVC中间件中才能读取到HttpContext.User的值。
            //1.先开启认证
            app.UseAuthentication();
            //2.再开启授权
            app.UseAuthorization();
            app.UseSession();
            app.UseResponseCaching();

            // 恢复/启动任务
            app.UseAddTaskSchedulers();

            app.UseMiddleware<GlobalExceptionMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        /// <summary>
        /// 注册Services服务
        /// </summary>
        /// <param name="services"></param>
        private void InjectServices(IServiceCollection services)
        {
            services.AddAppService();

            //开启计划任务
            services.AddTaskSchedulers();

            string connStr = Configuration.GetConnectionString(OptionsSetting.ConnAdmin);
            string connStrBus = Configuration.GetConnectionString(OptionsSetting.ConnBus);
            string dbKey = Configuration[OptionsSetting.DbKey];
            int dbType = Convert.ToInt32(Configuration[OptionsSetting.ConnDbType]);
            int dbType_bus = Convert.ToInt32(Configuration[OptionsSetting.ConnBusDbType]);

            SugarIocServices.AddSqlSugar(new List<IocConfig>() {
               new IocConfig() {
                ConfigId = "0",
                ConnectionString = connStr,
                DbType = (IocDbType)dbType,
                IsAutoCloseConnection = true//自动释放
            }, new IocConfig() {
                ConfigId = "1",
                ConnectionString = connStrBus,
                DbType = (IocDbType)dbType_bus,
                IsAutoCloseConnection = true//自动释放
            }
            });

            //调式代码 用来打印SQL 
            DbScoped.SugarScope.GetConnection(0).Aop.OnLogExecuting = (sql, pars) =>
            {
                var param = DbScoped.SugarScope.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value));
                //Console.WriteLine("【SQL语句】" + sql.ToLower() + "\r\n" + param);
                logger.Info($"Sql语句：{sql}，{param}");
            };
            //出错打印日志
            DbScoped.SugarScope.GetConnection(0).Aop.OnError = (e) =>
            {
                Console.WriteLine($"[执行Sql出错]{e.Message}，SQL={e.Sql}");
                Console.WriteLine();
            };

            //调式代码 用来打印SQL 
            DbScoped.SugarScope.GetConnection(1).Aop.OnLogExecuting = (sql, pars) =>
            {
                var param = DbScoped.SugarScope.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value));
                //Console.WriteLine("【SQL语句Bus】" + sql.ToLower() + "\r\n" + param);
                logger.Info($"Sql语句：{sql}, {param}");
            };
            //Bus Db错误日志
            DbScoped.SugarScope.GetConnection(1).Aop.OnError = (e) =>
            {
                logger.Error($"执行Sql语句失败：{e.Sql}，原因：{e.Message}");
            };
        }
    }
}
