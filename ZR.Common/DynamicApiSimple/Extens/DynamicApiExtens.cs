using System.Linq;
using System.Reflection;
using Infrastructure.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace ZR.Common.DynamicApiSimple.Extens
{
    public static class DynamicApiExtens
    {
        public static IServiceCollection AddDynamicApi(this IServiceCollection services)
        {
            services.AddMvc().ConfigureApplicationPartManager(m =>
            {
                foreach (Assembly assembly in AssemblyUtils.GetAssemblies())
                {
                    
                    if (m.ApplicationParts.Any(it => it.Name.Equals(assembly.FullName.Split(',')[0]))) continue;
                    
                    m.ApplicationParts.Add(new AssemblyPart(assembly));
                }
                m.FeatureProviders.Add(new ApiFeatureProvider());
            });

            services.Configure<MvcOptions>(o =>
            {
                o.Conventions.Add(new ApiConvention());
            });
            return services;
        }
    }
}
