using Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ZR.Admin.WebApi.Filters;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    /// <summary>
    /// 系统监控
    /// </summary>
    [ApiExplorerSettings(GroupName = "sys")]
    [Verify]
    public class MonitorController(IOptions<OptionsSetting> options, IWebHostEnvironment hostEnvironment) : BaseController
    {
        private OptionsSetting Options = options.Value;
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 获取缓存监控数据
        /// </summary>
        /// <returns></returns>
        [HttpGet("monitor/cache")]
        public IActionResult GetCache()
        {
            return SUCCESS(1);
        }

        /// <summary>
        /// 获取服务器信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("monitor/server")]
        //[AllowAnonymous]
        public IActionResult Server()
        {
            //核心数
            int cpuNum = Environment.ProcessorCount;
            string computerName = Environment.MachineName;
            string osName = RuntimeInformation.OSDescription;
            string osArch = RuntimeInformation.OSArchitecture.ToString();
            string version = RuntimeInformation.FrameworkDescription;
            string appRAM = ((double)Process.GetCurrentProcess().WorkingSet64 / 1048576).ToString("N2") + " MB";
            string startTime = Process.GetCurrentProcess().StartTime.ToString("yyyy-MM-dd HH:mm:ss");
            string sysRunTime = ComputerHelper.GetRunTime();
            string serverIP = Request.HttpContext.Connection.LocalIpAddress.MapToIPv4().ToString() + ":" + Request.HttpContext.Connection.LocalPort;//获取服务器IP

            var programStartTime = Process.GetCurrentProcess().StartTime;
            string programRunTime = DateTimeHelper.FormatTime((DateTime.Now - programStartTime).TotalMilliseconds.ToString().Split('.')[0].ParseToLong());
            var data = new
            {
                cpu = ComputerHelper.GetComputerInfo(),
                disk = ComputerHelper.GetDiskInfos(),
                sys = new { cpuNum, computerName, osName, osArch, serverIP, runTime = sysRunTime },
                app = new
                {
                    name = hostEnvironment.EnvironmentName,
                    rootPath = hostEnvironment.ContentRootPath,
                    webRootPath = hostEnvironment.WebRootPath,
                    version,
                    appRAM,
                    startTime,
                    runTime = programRunTime,
                    host = serverIP
                },
            };

            return SUCCESS(data);
        }
    }
}
