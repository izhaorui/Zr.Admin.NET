using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ZR.Admin.WebApi.Filters;
using ZR.Common;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    //[Verify]
    public class MonitorController : BaseController
    {
        private OptionsSetting Options;
        private IWebHostEnvironment HostEnvironment;

        public MonitorController(IOptions<OptionsSetting> options, IWebHostEnvironment hostEnvironment)
        {
            this.HostEnvironment = hostEnvironment;
            this.Options = options.Value;
        }

        /// <summary>
        /// 获取缓存监控数据
        /// </summary>
        /// <returns></returns>
        [HttpGet("monitor/cache")]
        public ApiResult GetCache()
        {
            string redis_connString = Options.Redis;
            var rs = new CSRedis.CSRedisClient(redis_connString);

            RedisHelper.Initialization(rs);//初始化Redis

            return ToJson(1);
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
            string runTime = Process.GetCurrentProcess().StartTime.ToString("yyyy-MM-dd HH:mm:ss");
            string serverIP = (Request.HttpContext.Connection.LocalIpAddress.MapToIPv4().ToString() + ":" + Request.HttpContext.Connection.LocalPort);//获取服务器IP

            var data = new
            {
                cpu = ComputerHelper.GetComputerInfo(),
                sys = new { cpuNum, computerName, osName, osArch, serverIP },
                app = new
                {
                    name = HostEnvironment.EnvironmentName,
                    rootPath = HostEnvironment.ContentRootPath,
                    webRootPath = HostEnvironment.WebRootPath,
                    version,
                    appRAM,
                    startTime,
                    runTime,
                    host = serverIP
                }
            };

            return SUCCESS(data);
        }
    }
}
