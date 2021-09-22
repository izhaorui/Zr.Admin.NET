using Infrastructure.Attribute;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace Infrastructure.Extensions
{
    public static class AppServiceExtensions
    {
        /// <summary>
        /// 注册引用程序域中所有有AppService标记的类的服务
        /// </summary>
        /// <param name="services"></param>
        public static void AddAppService(this IServiceCollection services)
        {
            //var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            string []cls = new string[] { "ZR.Repository", "ZR.Service", "ZR.Tasks" };
            foreach (var item in cls)
            {
                Assembly assembly = Assembly.Load(item);
                Register(services, assembly);
            }
        }

        private static void Register(IServiceCollection services, Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                var serviceAttribute = type.GetCustomAttribute<AppServiceAttribute>();

                if (serviceAttribute != null)
                {
                    var serviceType = serviceAttribute.ServiceType;
                    //情况1 适用于依赖抽象编程，注意这里只获取第一个
                    if (serviceType == null && serviceAttribute.InterfaceServiceType)
                    {
                        serviceType = type.GetInterfaces().FirstOrDefault();
                    }
                    //情况2 不常见特殊情况下才会指定ServiceType，写起来麻烦
                    if (serviceType == null)
                    {
                        serviceType = type;
                    }

                    switch (serviceAttribute.ServiceLifetime)
                    {
                        case LifeTime.Singleton:
                            services.AddSingleton(serviceType, type);
                            break;
                        case LifeTime.Scoped:
                            services.AddScoped(serviceType, type);
                            break;
                        case LifeTime.Transient:
                            services.AddTransient(serviceType, type);
                            break;
                        default:
                            services.AddTransient(serviceType, type);
                            break;
                    }
                    //Console.WriteLine($"注册：{serviceType}");
                }
                else
                {
                    //Console.WriteLine($"注册：{serviceType}");
                }
            }
        }
    }
}
