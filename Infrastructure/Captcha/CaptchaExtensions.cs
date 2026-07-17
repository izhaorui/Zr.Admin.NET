using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Captcha
{
    /// <summary>
    /// 验证码提供程序 DI 扩展
    /// </summary>
    public static class CaptchaExtensions
    {
        /// <summary>
        /// 注册验证码提供程序，默认使用 SVG 实现
        /// </summary>
        public static IServiceCollection AddCaptchaProvider(this IServiceCollection services)
        {
            services.AddSingleton<ICaptchaProvider, SvgCaptchaProvider>();
            return services;
        }
    }
}
