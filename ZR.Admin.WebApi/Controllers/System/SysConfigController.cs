using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Filters;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 配置文件
    /// </summary>
    [Verify]
    [Route("system/config")]
    public class SysConfigController : BaseController
    {
        [HttpGet("list")]
        public IActionResult Index()
        {
            return SUCCESS(1);
        }
    }
}
