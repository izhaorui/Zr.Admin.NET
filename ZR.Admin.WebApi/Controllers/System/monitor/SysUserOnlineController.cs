using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Hubs;
using ZR.Model;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    [Verify]
    [Route("monitor/online")]
    [Tags("在线用户SysUserOnline")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysUserOnlineController : BaseController
    {
        private IHubContext<Hub> HubContext;

        public SysUserOnlineController(IHubContext<Hub> hubContext)
        {
            HubContext = hubContext;
        }

        /// <summary>
        /// 获取在线用户列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult Index([FromQuery] PagerInfo parm)
        {
            var result = MessageHub.clientUsers
                .OrderByDescending(f => f.LoginTime)
                .Skip(parm.PageNum - 1).Take(parm.PageSize);

            return SUCCESS(new { result, totalNum = MessageHub.clientUsers.Count });
        }
    }
}
