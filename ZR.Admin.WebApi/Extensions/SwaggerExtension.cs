using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System;
using System.Collections.Generic;
using System.IO;

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
        }

        public static void AddSwaggerConfig(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            IWebHostEnvironment hostEnvironment = App.GetRequiredService<IWebHostEnvironment>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ZrAdmin.NET Api - .NET5",
                    Version = "v1",
                    Description = "",
                });
                try
                {
                    //添加文档注释
                    c.IncludeXmlComments(Path.Combine(hostEnvironment.ContentRootPath, "ZRAdmin.xml"), true);
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
            });
        }
    }
}
