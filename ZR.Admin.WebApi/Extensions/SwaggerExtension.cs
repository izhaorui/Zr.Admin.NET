using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Reflection;

namespace ZR.Admin.WebApi.Extensions
{
    public static class SwaggerExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        public static void UseSwagger(this IApplicationBuilder app)
        {
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "swagger/{documentName}/swagger.json";
                c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                {
                    var url = $"{httpReq.Scheme}://{httpReq.Host.Value}";
                    var referer = httpReq.Headers["Referer"].ToString();
                    if (referer.Contains(GlobalConstant.DevApiProxy))
                        url = referer[..(referer.IndexOf(GlobalConstant.DevApiProxy, StringComparison.InvariantCulture) + GlobalConstant.DevApiProxy.Length - 1)];
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
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("sys/swagger.json", "系统管理");
                c.SwaggerEndpoint("article/swagger.json", "文章管理");
                c.SwaggerEndpoint("v1/swagger.json", "business");
                c.DocExpansion(DocExpansion.None); //->修改界面打开时自动折叠
            });
        }

        public static void AddSwaggerConfig(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            //IWebHostEnvironment hostEnvironment = services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("sys", new OpenApiInfo
                {
                    Title = "ZrAdmin.NET Api",
                    Version = "v1",
                    Description = "系统管理",
                    Contact = new OpenApiContact { Name = "ZRAdmin doc", Url = new Uri("https://www.izhaorui.cn/doc") }
                });
                c.SwaggerDoc("article", new OpenApiInfo
                {
                    Title = "ZrAdmin.NET Api",
                    Version = "v1",
                    Description = "文章管理",
                    Contact = new OpenApiContact { Name = "ZRAdmin doc", Url = new Uri("https://www.izhaorui.cn/doc") }
                });
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ZrAdmin.NET Api",
                    Version = "v1",
                    Description = "",
                });
                try
                {
                    //var tempPath = hostEnvironment.ContentRootPath;
                    //添加文档注释
                    var baseDir = AppContext.BaseDirectory;
                    c.IncludeXmlComments(Path.Combine(baseDir, "ZR.Model.xml"), true);
                    c.IncludeXmlComments(Path.Combine(baseDir, "ZR.ServiceCore.xml"), true);
                    c.IncludeXmlComments(Path.Combine(baseDir, "ZR.Service.xml"), true);
                    c.IncludeXmlComments(Path.Combine(baseDir, "ZR.Admin.WebApi.xml"), true);

                    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var xmlPath = Path.Combine(baseDir, xmlFile);
                    c.IncludeXmlComments(xmlPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("swagger 文档加载失败" + ex.Message);
                }

                //参考文章：http://www.zyiz.net/tech/detail-134965.html
                //需要安装包Swashbuckle.AspNetCore.Filters
                // 开启权限小锁 需要在对应的Action上添加[Authorize]才能看到
                c.OperationFilter<AddResponseHeadersFilter>();
                c.OperationFilter<AppendAuthorizeToSummaryOperationFilter>();

                //在header 中添加token，传递到后台
                c.OperationFilter<SecurityRequirementsOperationFilter>();

                c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme,
                    new OpenApiSecurityScheme
                    {
                        In = ParameterLocation.Header,
                        Description = "请输入Login接口返回的Token，前置Bearer。示例：Bearer {Token}",
                        Name = "Authorization",//jwt默认的参数名称,
                        Type = SecuritySchemeType.ApiKey, //指定ApiKey
                        BearerFormat = "JWT",//标识承载令牌的格式 该信息主要是出于文档目的
                        Scheme = JwtBearerDefaults.AuthenticationScheme//授权中要使用的HTTP授权方案的名称
                    });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        new List<string>()
                    }
                });

                try
                {
                    //判断接口归于哪个分组
                    c.DocInclusionPredicate((docName, apiDescription) =>
                    {
                        if (docName == "v1")
                        {
                            //当分组为NoGroup时，只要没加特性的都属于这个组
                            return string.IsNullOrEmpty(apiDescription.GroupName);
                        }
                        else
                        {
                            return apiDescription.GroupName == docName;
                        }
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

            });
        }
    }
}
