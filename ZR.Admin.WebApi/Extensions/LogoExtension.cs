using Infrastructure.Helper;
using JinianNet.JNTemplate;

public static class LogoExtension
{
    private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

    public static void AddLogo(this IServiceCollection services)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        var contentTpl = JnHelper.ReadTemplate("", "logo.txt");
        var content = contentTpl?.Render();
        var url = AppSettings.GetConfig("urls");
        var useTenant = AppSettings.GetConfig("TenantSettings:UseTenant");

        var UploadOptions = AppSettings.Get<Upload>("Upload");
        Console.WriteLine(content);
        Console.ForegroundColor = ConsoleColor.Blue;
        //Console.WriteLine("🎉源码地址: https://gitee.com/izory/ZrAdminNetCore");
        logger.Debug("📖官方文档：http://www.izhaorui.cn");
        logger.Debug("💰打赏作者：http://www.izhaorui.cn/vip");
        logger.Debug("📱移动端体验：http://demo.izhaorui.cn/h5");
        logger.Debug($"Swagger地址：{url}/swagger/index.html");
        logger.Debug($"初始化说明：appsettings 设置 InitDb:true 启动将自动建表并导入种子数据（无需手动调用接口）");
        logger.Debug($"如需手动重导种子数据：{url}/common/InitSeedData");
        logger.Debug($"多租户是否启动：{useTenant}");
        logger.Debug($"本地上传地址：{UploadOptions?.UploadUrl}-{UploadOptions?.LocalSavePath}");
    }
}

