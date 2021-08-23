using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZR.Admin.WebApi.Controllers.System
{
    [Route("system/notice")]
    public class SysNoticeController : BaseController
    {
        [HttpGet("list")]
        public IActionResult Index()
        {
            return SUCCESS(null);
        }
    }
}
