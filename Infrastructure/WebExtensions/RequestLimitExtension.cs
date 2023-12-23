using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZR.Infrastructure.WebExtensions
{
    public static class RequestLimitExtension
    {
        /// <summary>
        /// 请求body大小设置
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void AddRequestLimit(this IServiceCollection services, IConfiguration configuration)
        {
            var sizeM = configuration.GetSection("upload:requestLimitSize").Get<int>();
            services.Configure<FormOptions>(x =>
            {
                x.MultipartBodyLengthLimit = sizeM * 1024 * 1024;
                x.MemoryBufferThreshold = sizeM * 1024 * 1024;
                x.ValueLengthLimit = int.MaxValue;
            });
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = sizeM * 1024 * 1024;
            });
            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = sizeM * 1024 * 1024; // 设置最大请求体大小为500MB
            });
        }
    }
}
