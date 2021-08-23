using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    [Verify]
    [Route("monitor/online")]
    public class SysUserOnlineController : BaseController
    {
        [HttpGet("list")]
        public IActionResult Index()
        {
            return SUCCESS(null);
        }
    }
}
