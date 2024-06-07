using Infrastructure.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Linq;
using System.Reflection;

namespace ZR.Common.DynamicApiSimple.Extens
{
    public static class DynamicApiExtens
    {
        public static string TIME_FORMAT_FULL = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// 注入动态api
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDynamicApi(this IServiceCollection services)
        {
            services.AddMvc()
                .ConfigureApplicationPartManager(m =>
            {
                foreach (Assembly assembly in AssemblyUtils.GetAssemblies())
                {

                    if (m.ApplicationParts.Any(it => it.Name.Equals(assembly.FullName.Split(',')[0]))) continue;

                    m.ApplicationParts.Add(new AssemblyPart(assembly));
                }
                m.FeatureProviders.Add(new ApiFeatureProvider());
            }).AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DateFormatString = TIME_FORMAT_FULL;
                options.SerializerSettings.Converters.Add(new IsoDateTimeConverter
                {
                    DateTimeFormat = TIME_FORMAT_FULL,
                });
                // 设置为驼峰命名
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            });

            services.Configure<MvcOptions>(o =>
            {
                o.Conventions.Add(new ApiConvention());
            });
            return services;
        }
    }
}
