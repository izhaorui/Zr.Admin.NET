using Infrastructure.Helper;
using JinianNet.JNTemplate;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Infrastructure
{
    public static class LogoExtension
    {
        public static void AddLogo(this IServiceCollection services)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            var contentTpl = JnHelper.ReadTemplate("", "logo.txt");
            var content = contentTpl?.Render();
            var context = App.HttpContext;
            Console.WriteLine(content);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("🎉源码地址: https://gitee.com/izory/ZrAdminNetCore");
            Console.WriteLine("📖官方文档：http://www.izhaorui.cn/doc");
            Console.WriteLine("💰打赏作者：http://www.izhaorui.cn/doc/support.html");
            Console.WriteLine("📱移动端体验：http://www.izhaorui.cn/h5");
            Console.WriteLine($"Swagger地址：[后端启动地址]/swagger/index.html");
        }
    }
}
